using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Security;
using Plastoids.Infrastructure.Sms;

namespace Plastoids.IntegrationTests;

/// <summary>Helpers for the identity flows used across integration tests.</summary>
internal static class ApiClientExtensions
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public sealed record Tokens(bool MfaRequired, string? MfaToken, string? AccessToken, string? RefreshToken, DateTime? ExpiresAt);

    public sealed record VerifyOtpResult(bool IsNewAccount, Guid UserId, Tokens Tokens);

    /// <summary>Request + verify an OTP for a (new or existing) phone; returns the signed-in client.</summary>
    public static async Task<(HttpClient Client, VerifyOtpResult Result)> SignInWithPhoneAsync(this PlastoidsApiFactory factory, string phone, string displayName = "Test Learner", string dob = "1999-05-05")
    {
        var client = factory.CreateClient();
        var request = await client.PostAsJsonAsync("/api/v1/identity/auth/otp/request", new { phone });
        request.StatusCode.Should().Be(System.Net.HttpStatusCode.Accepted, await request.Content.ReadAsStringAsync());

        var code = factory.LastOtpCodeFor(phone);
        var verify = await client.PostAsJsonAsync("/api/v1/identity/auth/otp/verify", new { phone, code, dateOfBirth = dob, displayName });
        var body = await verify.Content.ReadAsStringAsync();
        verify.IsSuccessStatusCode.Should().BeTrue(body);
        var result = JsonSerializer.Deserialize<VerifyOtpResult>(body, Json)!;
        client.UseBearer(result.Tokens.AccessToken!);
        return (client, result);
    }

    public static string LastOtpCodeFor(this PlastoidsApiFactory factory, string phone)
    {
        Plastoids.Modules.Identity.Domain.PhoneNumber.TryNormalise(phone, out var e164);
        var sms = factory.Services.GetRequiredService<FakeSmsSender>().LastTo(e164);
        sms.Should().NotBeNull($"an SMS should have been sent to {e164}");
        var match = System.Text.RegularExpressions.Regex.Match(sms!.Message, @"\b(\d{6})\b");
        match.Success.Should().BeTrue(sms.Message);
        return match.Groups[1].Value;
    }

    /// <summary>Staff login: password grant → MFA challenge → TOTP from the seeded secret.</summary>
    public static async Task<HttpClient> SignInAsStaffAsync(this PlastoidsApiFactory factory)
    {
        var client = factory.CreateClient();
        var first = await client.PostAsJsonAsync("/api/v1/identity/auth/token", new { grantType = "password", email = PlastoidsApiFactory.StaffEmail, password = PlastoidsApiFactory.StaffPassword });
        var challenge = await first.Content.ReadFromJsonAsync<Tokens>(Json);
        first.IsSuccessStatusCode.Should().BeTrue(await first.Content.ReadAsStringAsync());
        challenge!.MfaRequired.Should().BeTrue("the seeded staff user has MFA enabled");

        var code = Totp.Compute(Base32.Decode(PlastoidsApiFactory.StaffMfaSecret), DateTime.UtcNow);
        var second = await client.PostAsJsonAsync("/api/v1/identity/auth/mfa/verify", new { code, mfaToken = challenge.MfaToken });
        var tokens = await second.Content.ReadFromJsonAsync<Tokens>(Json);
        second.IsSuccessStatusCode.Should().BeTrue(await second.Content.ReadAsStringAsync());
        client.UseBearer(tokens!.AccessToken!);
        return client;
    }

    public static void UseBearer(this HttpClient client, string token)
        => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public static string NewPhone()
    {
        var n = Random.Shared.NextInt64(1_000_000_000, 9_999_999_999);
        return "+234" + n.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout, string what)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting for {what}.");
            }

            await Task.Delay(250);
        }
    }
}
