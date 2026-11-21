using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static Plastoids.IntegrationTests.ApiClientExtensions;

namespace Plastoids.IntegrationTests;

/// <summary>
/// Story I1. Only one staff account is seeded (PlastoidsApiFactory.StaffEmail), so a second-approver success path for four-eyes
/// actions is covered by the unit tests (FourEyesTests); here the same staff member is refused as checker.
/// </summary>
[Collection(ApiCollection.Name)]
public class BackOfficeTests(PlastoidsApiFactory factory)
{
    private static void Key(HttpClient client)
    {
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
    }

    [Fact]
    public async Task Dashboard_queues_flags_fee_rules_and_audit_log_serve_the_back_office()
    {
        var (tutor, tutorUserId, slug, _) = await CatalogueTests.PublishedCourseAsync(factory);
        var (learner, learnerSignIn) = await factory.SignInWithPhoneAsync(NewPhone(), "Back office learner");
        await LearningAndCertificatesTests.EnrolByPaymentAsync(factory, learner, slug);
        var staff = await factory.SignInAsStaffAsync();

        // Executive dashboard: GMV and enrolments arrive through projections; revenue and liabilities from the ledger.
        JsonElement dashboard = default;
        await WaitUntilAsync(async () =>
        {
            dashboard = await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/backoffice/dashboard", Json);
            return dashboard.GetProperty("revenue").GetProperty("gmv").GetProperty("amount").GetInt64() >= 4_500_000
                && dashboard.GetProperty("programmes").GetProperty("enrolmentsCreated").GetInt64() >= 1;
        }, TimeSpan.FromSeconds(60), "dashboard projections");
        dashboard.GetProperty("revenue").GetProperty("courseCommission").GetProperty("amount").GetInt64().Should().BeGreaterThanOrEqualTo(900_000);
        dashboard.GetProperty("liabilities").GetProperty("tutorPayable").GetProperty("amount").GetInt64().Should().BeGreaterThanOrEqualTo(0);
        dashboard.GetProperty("participants").GetProperty("learnersRegistered").GetInt64().Should().BeGreaterThanOrEqualTo(1);
        dashboard.GetProperty("payouts").GetProperty("payoutsFrozen").ValueKind.Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);

        // Users, tutors, sponsors.
        var users = await staff.GetFromJsonAsync<JsonElement>($"/api/v1/admin/backoffice/users?query={learnerSignIn.UserId}", Json);
        users.EnumerateArray().Should().ContainSingle(u => u.GetProperty("userId").GetGuid() == learnerSignIn.UserId);
        var tutors = await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/backoffice/tutors?status=verified", Json);
        tutors.EnumerateArray().Should().Contain(t => t.GetProperty("userId").GetGuid() == tutorUserId);
        (await staff.GetAsync("/api/v1/admin/backoffice/sponsors?verificationStatus=pending")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Suspend + reinstate (audited), and the account cannot refresh meanwhile.
        Key(staff);
        var suspended = await staff.PostAsJsonAsync($"/api/v1/admin/backoffice/users/{learnerSignIn.UserId}/suspend", new { reason = "Chargeback investigation" });
        suspended.StatusCode.Should().Be(HttpStatusCode.OK, await suspended.Content.ReadAsStringAsync());
        (await suspended.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("status").GetString().Should().Be("suspended");
        var refresh = await factory.CreateClient().PostAsJsonAsync("/api/v1/identity/auth/token", new { grantType = "refresh_token", refreshToken = learnerSignIn.Tokens.RefreshToken });
        refresh.IsSuccessStatusCode.Should().BeFalse("sessions are revoked on suspension");
        Key(staff);
        (await staff.PostAsync($"/api/v1/admin/backoffice/users/{learnerSignIn.UserId}/reinstate", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Queues are views only.
        (await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/backoffice/queues/verification", Json)).GetProperty("decideTutorsAt").GetString().Should().Contain("/admin/tutors");
        (await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/backoffice/queues/course-moderation", Json)).GetProperty("courses").ValueKind.Should().Be(JsonValueKind.Array);
        (await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/backoffice/queues/refunds", Json)).GetProperty("refunds").ValueKind.Should().Be(JsonValueKind.Array);
        (await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/backoffice/queues/disputes", Json)).GetProperty("holds").ValueKind.Should().Be(JsonValueKind.Array);
        (await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/backoffice/data-requests", Json)).ValueKind.Should().Be(JsonValueKind.Array);

        // Fee rules: four-eyes refuses the proposer as checker (a second staff account would succeed; see FourEyesTests).
        Key(staff);
        var proposed = await staff.PostAsJsonAsync("/api/v1/admin/payments/fee-rules", new { ruleCode = "course_platform", commissionBps = 1800, effectiveFrom = DateTime.UtcNow.AddDays(1), rationale = "Launch promotion for Q1" });
        proposed.StatusCode.Should().Be(HttpStatusCode.Created, await proposed.Content.ReadAsStringAsync());
        var ruleId = (await proposed.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("ruleId").GetGuid();
        Key(staff);
        var selfApproval = await staff.PostAsJsonAsync($"/api/v1/admin/payments/fee-rules/{ruleId}/decision", new { decision = "approved" });
        selfApproval.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await selfApproval.Content.ReadAsStringAsync()).Should().Contain("maker_checker_violation");
        var feeRules = await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/payments/fee-rules", Json);
        feeRules.GetProperty("effectiveNow").EnumerateArray().Single(r => r.GetProperty("ruleCode").GetString() == "course_platform").GetProperty("source").GetString().Should().Be("default");
        feeRules.GetProperty("rules").EnumerateArray().Should().Contain(r => r.GetProperty("ruleId").GetGuid() == ruleId && r.GetProperty("status").GetString() == "proposed");

        // Role grants: same four-eyes rule.
        Key(staff);
        var grant = await staff.PostAsJsonAsync("/api/v1/admin/identity/role-grants", new { userId = tutorUserId, role = "sponsor", reason = "Runs a CSR programme" });
        grant.StatusCode.Should().Be(HttpStatusCode.Created, await grant.Content.ReadAsStringAsync());
        var grantId = (await grant.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("proposalId").GetGuid();
        Key(staff);
        (await staff.PostAsJsonAsync($"/api/v1/admin/identity/role-grants/{grantId}/decision", new { decision = "approved" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Feature flags round trip.
        Key(staff);
        var flag = await staff.PutAsJsonAsync("/api/v1/admin/backoffice/feature-flags/ai-tutor", new { enabled = true, description = "AI tutor rollout" });
        flag.StatusCode.Should().Be(HttpStatusCode.OK, await flag.Content.ReadAsStringAsync());
        var flags = await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/backoffice/feature-flags", Json);
        flags.EnumerateArray().Should().Contain(f => f.GetProperty("key").GetString() == "ai-tutor" && f.GetProperty("enabled").GetBoolean());
        Key(staff);
        (await staff.PutAsJsonAsync("/api/v1/admin/backoffice/feature-flags/Bad Key", new { enabled = true })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // Audit log lists the mutations (audit.event.v1 is consumed asynchronously).
        await WaitUntilAsync(async () =>
        {
            var log = await staff.GetFromJsonAsync<JsonElement>("/api/v1/admin/backoffice/audit-log?entity=FeatureFlag&limit=50", Json);
            return log.GetProperty("items").EnumerateArray().Any(e => (e.GetProperty("after").GetString() ?? string.Empty).Contains("ai-tutor"));
        }, TimeSpan.FromSeconds(60), "feature flag audit entry");
        var userLog = await staff.GetFromJsonAsync<JsonElement>($"/api/v1/admin/backoffice/audit-log?entity=User&entityId={learnerSignIn.UserId}", Json);
        userLog.GetProperty("items").EnumerateArray().Should().Contain(e => (e.GetProperty("after").GetString() ?? string.Empty).Contains("Suspended") || (e.GetProperty("before").GetString() ?? string.Empty).Contains("Suspended"));
        tutor.Dispose();
    }
}
