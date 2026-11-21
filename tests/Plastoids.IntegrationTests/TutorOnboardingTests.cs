using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Security;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.SharedKernel.Tenancy;
using static Plastoids.IntegrationTests.ApiClientExtensions;

namespace Plastoids.IntegrationTests;

[Collection(ApiCollection.Name)]
public class TutorOnboardingTests(PlastoidsApiFactory factory)
{
    /// <summary>Signs in a fresh learner, applies as tutor, refreshes the token (tutor role) and completes MFA (tutor policy).</summary>
    internal static async Task<(HttpClient Client, Guid UserId, Guid TutorId)> ApplyAsTutorAsync(PlastoidsApiFactory factory, string displayName)
    {
        var (client, signIn) = await factory.SignInWithPhoneAsync(NewPhone(), displayName, "1992-03-03");
        var apply = await client.PostAsJsonAsync("/api/v1/tutors/applications", new
        {
            headline = "Senior data analyst teaching SQL",
            bio = "Ten years of analytics experience in banking. I teach SQL, Python and dashboards with real datasets and weekly projects.",
            subjects = new[] { "sql", "python" },
            languages = new[] { "en" },
            formats = new[] { "one_to_one", "course" },
            timeZone = "Africa/Lagos",
            hourlyRate = new { amount = 800000, currency = "NGN" },
            acceptCodeOfConduct = true,
        });
        apply.StatusCode.Should().Be(HttpStatusCode.Created, await apply.Content.ReadAsStringAsync());
        var tutorId = (await apply.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("tutorId").GetGuid();

        // Tutor endpoints need the tutor role (fresh token) and MFA.
        var enable = await (await client.PostAsync("/api/v1/identity/me/mfa/enable", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        var code = Totp.Compute(Base32.Decode(enable.GetProperty("secret").GetString()!), DateTime.UtcNow);
        var confirm = await client.PostAsJsonAsync("/api/v1/identity/auth/mfa/verify", new { code });
        var tokens = await confirm.Content.ReadFromJsonAsync<Tokens>(Json);
        confirm.IsSuccessStatusCode.Should().BeTrue(await confirm.Content.ReadAsStringAsync());
        client.UseBearer(tokens!.AccessToken!);
        return (client, signIn.UserId, tutorId);
    }

    [Fact]
    public async Task Tutor_is_verified_only_after_identity_qualifications_and_subject_review_and_catalogue_learns_about_it()
    {
        var (tutor, userId, tutorId) = await ApplyAsTutorAsync(factory, "Ngozi");

        var dashboard = await tutor.GetFromJsonAsync<JsonElement>("/api/v1/tutors/me", Json);
        dashboard.GetProperty("status").GetString().Should().Be("applied");

        // Stage 1: identity via the fake KYC provider (NIN ending in 0 fails).
        var failed = await tutor.PostAsJsonAsync("/api/v1/tutors/me/identity-check", new { nin = "12345678900", bvn = "22345678901", selfieBlobKey = "selfies/x.jpg", fullName = "Ngozi Adaeze Eze" });
        failed.StatusCode.Should().Be(HttpStatusCode.OK);
        (await failed.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("passed").GetBoolean().Should().BeFalse();
        var passed = await tutor.PostAsJsonAsync("/api/v1/tutors/me/identity-check", new { nin = "12345678901", bvn = "22345678901", selfieBlobKey = "selfies/x.jpg", fullName = "Ngozi Adaeze Eze" });
        (await passed.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("passed").GetBoolean().Should().BeTrue();

        // Stage 2: credential upload + staff review (SLA 3 working days).
        var credential = await tutor.PostAsJsonAsync("/api/v1/tutors/me/credentials", new { type = "degree", issuer = "University of Ibadan", title = "BSc Statistics", contentType = "application/pdf" });
        credential.StatusCode.Should().Be(HttpStatusCode.Created, await credential.Content.ReadAsStringAsync());
        var credentialBody = await credential.Content.ReadFromJsonAsync<JsonElement>(Json);
        credentialBody.GetProperty("uploadUrl").GetString().Should().Contain("credentials/");
        var credentialId = credentialBody.GetProperty("credentialId").GetGuid();

        // Stage 3: demo lesson.
        var demo = await tutor.PostAsJsonAsync("/api/v1/tutors/me/demo-lesson", new { contentType = "video/mp4" });
        demo.StatusCode.Should().Be(HttpStatusCode.Accepted, await demo.Content.ReadAsStringAsync());

        // Payout account requires a name match with the verified identity.
        var resolver = factory.Services.GetRequiredService<FakeBankAccountResolver>();
        resolver.KnownAccounts["0123456789"] = "EZE NGOZI ADAEZE";
        resolver.KnownAccounts["0123456788"] = "TUNDE BAKARE";
        var mismatch = await tutor.PutAsJsonAsync("/api/v1/tutors/me/payout-account", new { bankCode = "058", accountNumber = "0123456788" });
        mismatch.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await mismatch.Content.ReadAsStringAsync()).Should().Contain("payout_name_mismatch");
        var payout = await tutor.PutAsJsonAsync("/api/v1/tutors/me/payout-account", new { bankCode = "058", accountNumber = "0123456789" });
        payout.StatusCode.Should().Be(HttpStatusCode.OK, await payout.Content.ReadAsStringAsync());
        (await payout.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("accountNumberMasked").GetString().Should().Be("******6789");

        // Not discoverable yet.
        var before = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/tutors?skill=sql", Json);
        before.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("tutorId").GetGuid()).Should().NotContain(tutorId);

        var staff = await factory.SignInAsStaffAsync();
        staff.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var reviewed = await staff.PostAsJsonAsync($"/api/v1/admin/tutors/credentials/{credentialId}/review", new { decision = "approved" });
        reviewed.StatusCode.Should().Be(HttpStatusCode.OK, await reviewed.Content.ReadAsStringAsync());
        (await reviewed.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("qualificationsStagePassed").GetBoolean().Should().BeTrue();

        staff.DefaultRequestHeaders.Remove("Idempotency-Key");
        staff.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var subject = await staff.PostAsJsonAsync($"/api/v1/admin/tutors/{tutorId}/subject-review", new { decision = "approved", score = 88 });
        subject.StatusCode.Should().Be(HttpStatusCode.OK, await subject.Content.ReadAsStringAsync());
        (await subject.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("verified").GetBoolean().Should().BeTrue();

        dashboard = await tutor.GetFromJsonAsync<JsonElement>("/api/v1/tutors/me", Json);
        dashboard.GetProperty("status").GetString().Should().Be("verified");
        dashboard.GetProperty("verification").EnumerateArray().Select(s => s.GetProperty("status").GetString()).Should().AllBe("passed");

        // Availability + pricing, then public discovery and slots.
        var availability = await tutor.PutAsJsonAsync("/api/v1/tutors/me/availability", new
        {
            timeZone = "Africa/Lagos",
            weekly = new[] { new { day = "Monday", start = "09:00", end = "12:00" }, new { day = "Saturday", start = "10:00", end = "14:00" } },
            exceptions = Array.Empty<object>(),
        });
        availability.StatusCode.Should().Be(HttpStatusCode.OK, await availability.Content.ReadAsStringAsync());
        (await tutor.PutAsJsonAsync("/api/v1/tutors/me/pricing", new { hourlyRate = new { amount = 900000, currency = "NGN" }, rules = new[] { new { format = "group", durationMinutes = 90, price = new { amount = 400000, currency = "NGN" }, maxLearners = 8 } } })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await tutor.PostAsJsonAsync("/api/v1/tutors/me/calendar/google", new { authorizationCode = "4/abc" })).StatusCode.Should().Be(HttpStatusCode.OK);

        var discovery = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/tutors?skill=sql&maxRate=1000000&lang=en&format=course&q=analyst", Json);
        discovery.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("tutorId").GetGuid()).Should().Contain(tutorId);
        var tooExpensive = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/tutors?skill=sql&maxRate=100", Json);
        tooExpensive.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("tutorId").GetGuid()).Should().NotContain(tutorId);

        var slots = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/tutors/{tutorId}/availability?days=14", Json);
        slots.GetProperty("slots").GetArrayLength().Should().BeGreaterThan(0);
        slots.GetProperty("timeZone").GetString().Should().Be("Africa/Lagos");

        // tutors.tutor-verified.v1 reached Kafka and Catalogue projected it.
        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var catalogue = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            await WaitUntilAsync(async () => await catalogue.VerifiedTutors.AsNoTracking().AnyAsync(t => t.TutorId == tutorId && t.UserId == userId), TimeSpan.FromSeconds(60), "catalogue to learn about the verified tutor");
        }
    }

    [Fact]
    public async Task Tutor_endpoints_require_the_tutor_role_with_mfa()
    {
        var (learner, _) = await factory.SignInWithPhoneAsync(NewPhone(), "Plain learner");
        (await learner.GetAsync("/api/v1/tutors/me")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
