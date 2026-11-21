using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Security;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Tenancy;
using static Plastoids.IntegrationTests.ApiClientExtensions;

namespace Plastoids.IntegrationTests;

[Collection(ApiCollection.Name)]
public class IdentityAuthTests(PlastoidsApiFactory factory)
{
    [Fact]
    public async Task Phone_otp_creates_the_account_only_after_verification_and_publishes_user_registered()
    {
        var phone = NewPhone();
        var anonymous = factory.CreateClient();

        // Request: 202, SMS captured, nothing persisted yet.
        var request = await anonymous.PostAsJsonAsync("/api/v1/identity/auth/otp/request", new { phone });
        request.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var code = factory.LastOtpCodeFor(phone);
        (await UserCountForPhoneAsync(phone)).Should().Be(0, "the account must not exist before verification");

        // Wrong code → 422 otp_invalid.
        var wrong = await anonymous.PostAsJsonAsync("/api/v1/identity/auth/otp/verify", new { phone, code = code == "000000" ? "000001" : "000000", dateOfBirth = "1999-01-01", displayName = "Ada" });
        wrong.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await wrong.Content.ReadAsStringAsync()).Should().Contain("otp_invalid");

        // Right code, but a new phone without registration details → 422.
        var noDetails = await anonymous.PostAsJsonAsync("/api/v1/identity/auth/otp/verify", new { phone, code });
        noDetails.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await noDetails.Content.ReadAsStringAsync()).Should().Contain("registration_details_required");

        // Code was consumed by the successful verification → must request again.
        var again = await anonymous.PostAsJsonAsync("/api/v1/identity/auth/otp/request", new { phone });
        again.StatusCode.Should().Be(HttpStatusCode.Accepted);
        code = factory.LastOtpCodeFor(phone);

        var created = await anonymous.PostAsJsonAsync("/api/v1/identity/auth/otp/verify", new { phone, code, dateOfBirth = "1999-01-01", displayName = "Ada" });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var result = JsonSerializer.Deserialize<VerifyOtpResult>(await created.Content.ReadAsStringAsync(), Json)!;
        result.IsNewAccount.Should().BeTrue();
        result.Tokens.AccessToken.Should().NotBeNullOrEmpty();
        result.Tokens.RefreshToken.Should().NotBeNullOrEmpty();

        // The outbox row for identity.user-registered.v1 gets published.
        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var row = await db.Outbox.SingleAsync(m => m.PartitionKey == result.UserId.ToString("D") && m.Topic == "plastoids.identity.user-registered.v1");
            row.Payload.Should().Contain("learner").And.NotContain(phone, "only the blind index leaves the module");
            await WaitUntilAsync(async () => (await db.Outbox.AsNoTracking().SingleAsync(m => m.Id == row.Id)).PublishedAt != null, TimeSpan.FromSeconds(30), "user-registered to be published");
        }

        // /me works with the token and masks the phone.
        anonymous.UseBearer(result.Tokens.AccessToken!);
        var me = await anonymous.GetFromJsonAsync<JsonElement>("/api/v1/identity/me", Json);
        me.GetProperty("displayName").GetString().Should().Be("Ada");
        me.GetProperty("phoneMasked").GetString().Should().Contain("****").And.NotBe(phone);
        me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).Should().Contain("learner");
    }

    [Fact]
    public async Task Under_18_cannot_register()
    {
        var phone = NewPhone();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/v1/identity/auth/otp/request", new { phone });
        var code = factory.LastOtpCodeFor(phone);
        var response = await client.PostAsJsonAsync("/api/v1/identity/auth/otp/verify", new { phone, code, dateOfBirth = DateTime.UtcNow.AddYears(-17).ToString("yyyy-MM-dd"), displayName = "Kid" });
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync()).Should().Contain("under_18");
        (await UserCountForPhoneAsync(phone)).Should().Be(0);
    }

    [Fact]
    public async Task Otp_requests_are_limited_to_five_per_hour_per_phone()
    {
        var phone = NewPhone();
        var client = factory.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            (await client.PostAsJsonAsync("/api/v1/identity/auth/otp/request", new { phone })).StatusCode.Should().Be(HttpStatusCode.Accepted, $"request {i + 1}");
        }

        var sixth = await client.PostAsJsonAsync("/api/v1/identity/auth/otp/request", new { phone });
        sixth.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await sixth.Content.ReadAsStringAsync()).Should().Contain("otp_rate_limited");
    }

    [Fact]
    public async Task Email_password_login_refresh_rotation_and_mfa_enrolment()
    {
        var phone = NewPhone();
        var (client, signIn) = await factory.SignInWithPhoneAsync(phone, "Tunde");
        var email = $"tunde-{Guid.NewGuid():N}@example.com";

        (await client.PostAsJsonAsync("/api/v1/identity/auth/register-email", new { email, password = "Correct-Horse-2026" })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var anonymous = factory.CreateClient();
        var bad = await anonymous.PostAsJsonAsync("/api/v1/identity/auth/token", new { grantType = "password", email, password = "nope" });
        bad.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var login = await anonymous.PostAsJsonAsync("/api/v1/identity/auth/token", new { grantType = "password", email, password = "Correct-Horse-2026" });
        var tokens = await login.Content.ReadFromJsonAsync<Tokens>(Json);
        login.StatusCode.Should().Be(HttpStatusCode.OK, await login.Content.ReadAsStringAsync());
        tokens!.MfaRequired.Should().BeFalse();

        // Refresh rotates; the old refresh token is dead afterwards.
        var refreshed = await anonymous.PostAsJsonAsync("/api/v1/identity/auth/token", new { grantType = "refresh_token", refreshToken = tokens.RefreshToken });
        refreshed.StatusCode.Should().Be(HttpStatusCode.OK, await refreshed.Content.ReadAsStringAsync());
        var rotated = await refreshed.Content.ReadFromJsonAsync<Tokens>(Json);
        rotated!.RefreshToken.Should().NotBe(tokens.RefreshToken);
        var reuse = await anonymous.PostAsJsonAsync("/api/v1/identity/auth/token", new { grantType = "refresh_token", refreshToken = tokens.RefreshToken });
        reuse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // MFA enrolment: enable → secret + recovery codes; verify with a TOTP code → tokens carry amr=mfa.
        var enable = await client.PostAsync("/api/v1/identity/me/mfa/enable", null);
        enable.StatusCode.Should().Be(HttpStatusCode.OK, await enable.Content.ReadAsStringAsync());
        var enrolment = await enable.Content.ReadFromJsonAsync<JsonElement>(Json);
        var secret = enrolment.GetProperty("secret").GetString()!;
        var recoveryCodes = enrolment.GetProperty("recoveryCodes").EnumerateArray().Select(c => c.GetString()!).ToList();
        recoveryCodes.Should().HaveCount(10);

        var totp = Totp.Compute(Base32.Decode(secret), DateTime.UtcNow);
        var confirm = await client.PostAsJsonAsync("/api/v1/identity/auth/mfa/verify", new { code = totp });
        confirm.StatusCode.Should().Be(HttpStatusCode.OK, await confirm.Content.ReadAsStringAsync());

        // Password login now returns an MFA challenge, which a recovery code can complete once.
        var challenge = await (await anonymous.PostAsJsonAsync("/api/v1/identity/auth/token", new { grantType = "password", email, password = "Correct-Horse-2026" })).Content.ReadFromJsonAsync<Tokens>(Json);
        challenge!.MfaRequired.Should().BeTrue();
        challenge.AccessToken.Should().BeNull();

        var viaRecovery = await anonymous.PostAsJsonAsync("/api/v1/identity/auth/mfa/verify", new { code = recoveryCodes[0], mfaToken = challenge.MfaToken });
        viaRecovery.StatusCode.Should().Be(HttpStatusCode.OK, await viaRecovery.Content.ReadAsStringAsync());
        var challenge2 = await (await anonymous.PostAsJsonAsync("/api/v1/identity/auth/token", new { grantType = "password", email, password = "Correct-Horse-2026" })).Content.ReadFromJsonAsync<Tokens>(Json);
        var reusedRecovery = await anonymous.PostAsJsonAsync("/api/v1/identity/auth/mfa/verify", new { code = recoveryCodes[0], mfaToken = challenge2!.MfaToken });
        reusedRecovery.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "a recovery code is single-use");

        // An MFA challenge token is not an access token.
        var misuse = factory.CreateClient();
        misuse.UseBearer(challenge2.MfaToken!);
        (await misuse.GetAsync("/api/v1/identity/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        signIn.UserId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Profile_goals_and_account_deletion()
    {
        var phone = NewPhone();
        var (client, signIn) = await factory.SignInWithPhoneAsync(phone, "Amina");

        (await client.PatchAsJsonAsync("/api/v1/identity/me", new { displayName = "Amina Y." })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PutAsJsonAsync("/api/v1/identity/me/goals", new { careerGoal = "Cloud engineer", interests = new[] { "cloud", "linux", "cloud" } })).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/identity/me", Json);
        me.GetProperty("displayName").GetString().Should().Be("Amina Y.");
        me.GetProperty("careerGoal").GetString().Should().Be("Cloud engineer");
        me.GetProperty("interests").EnumerateArray().Should().HaveCount(2);

        var wrongConfirmation = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/identity/me") { Content = JsonContent.Create(new { confirmation = "yes" }) });
        wrongConfirmation.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var deleted = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/v1/identity/me") { Content = JsonContent.Create(new { confirmation = "DELETE" }) });
        deleted.StatusCode.Should().Be(HttpStatusCode.NoContent, await deleted.Content.ReadAsStringAsync());

        (await client.GetAsync("/api/v1/identity/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized, "an anonymised account is inactive");

        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == signIn.UserId);
            user.Status.Should().Be(Plastoids.Modules.Identity.Domain.UserStatus.Deleted);
            user.PhoneCiphertext.Should().BeEmpty();
            user.DisplayName.Should().Be("Deleted user");
            (await db.RefreshTokens.CountAsync(t => t.UserId == signIn.UserId && t.RevokedAt == null)).Should().Be(0);
        }

        // The same phone can register again (the blind index was released).
        var (_, second) = await factory.SignInWithPhoneAsync(phone, "Amina again");
        second.IsNewAccount.Should().BeTrue();
        second.UserId.Should().NotBe(signIn.UserId);
    }

    private async Task<int> UserCountForPhoneAsync(string phone)
    {
        Plastoids.Modules.Identity.Domain.PhoneNumber.TryNormalise(phone, out var e164);
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var hash = scope.ServiceProvider.GetRequiredService<IFieldEncryptor>().BlindIndex(e164);
        return await db.Users.CountAsync(u => u.PhoneHash == hash);
    }
}
