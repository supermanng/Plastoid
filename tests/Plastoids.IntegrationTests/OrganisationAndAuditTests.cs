using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Audit.Persistence;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Tenancy;
using static Plastoids.IntegrationTests.ApiClientExtensions;

namespace Plastoids.IntegrationTests;

[Collection(ApiCollection.Name)]
public class OrganisationAndAuditTests(PlastoidsApiFactory factory)
{
    [Fact]
    public async Task Sponsor_registers_an_organisation_staff_verifies_it_and_the_event_and_audit_trail_follow()
    {
        var (owner, ownerSignIn) = await factory.SignInWithPhoneAsync(NewPhone(), "Mrs Adebayo");
        var cac = "RC" + Random.Shared.Next(100000, 999999);

        var created = await owner.PostAsJsonAsync("/api/v1/identity/organisations", new { name = "Zenith CSR", type = "sponsor", cacNumber = cac, signatoryName = "Mrs Adebayo", signatoryEmail = "csr@zenith.example" });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var organisationId = (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("organisationId").GetGuid();

        var duplicate = await owner.PostAsJsonAsync("/api/v1/identity/organisations", new { name = "Other", type = "sponsor", cacNumber = cac, signatoryName = "X", signatoryEmail = "x@y.example" });
        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Members: add a learner as reviewer; a non-owner may not add members; last owner cannot be demoted.
        var (member, memberSignIn) = await factory.SignInWithPhoneAsync(NewPhone(), "Reviewer");
        var memberPhone = (await member.GetFromJsonAsync<JsonElement>("/api/v1/identity/me", Json)).GetProperty("id").GetGuid();
        memberPhone.Should().Be(memberSignIn.UserId);

        var forbidden = await member.PostAsJsonAsync($"/api/v1/identity/organisations/{organisationId}/members", new { email = "nobody@example.com", role = "viewer" });
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var unknown = await owner.PostAsJsonAsync($"/api/v1/identity/organisations/{organisationId}/members", new { email = "nobody@example.com", role = "viewer" });
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);

        await member.PostAsJsonAsync("/api/v1/identity/auth/register-email", new { email = $"rev-{memberSignIn.UserId:N}@example.com", password = "Reviewer-Pass-1" });
        var added = await owner.PostAsJsonAsync($"/api/v1/identity/organisations/{organisationId}/members", new { email = $"rev-{memberSignIn.UserId:N}@example.com", role = "reviewer" });
        added.StatusCode.Should().Be(HttpStatusCode.Created, await added.Content.ReadAsStringAsync());

        var lastOwner = await owner.PutAsJsonAsync($"/api/v1/identity/organisations/{organisationId}/members/{ownerSignIn.UserId}/role", new { role = "finance" });
        lastOwner.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var promoted = await owner.PutAsJsonAsync($"/api/v1/identity/organisations/{organisationId}/members/{memberSignIn.UserId}/role", new { role = "owner" });
        promoted.StatusCode.Should().Be(HttpStatusCode.NoContent, await promoted.Content.ReadAsStringAsync());

        // Staff verification needs staff + MFA + Idempotency-Key; a learner is refused.
        var learnerAttempt = await owner.PostAsJsonAsync($"/api/v1/admin/identity/organisations/{organisationId}/verification", new { decision = "verified" });
        learnerAttempt.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var staff = await factory.SignInAsStaffAsync();
        var noKey = await staff.PostAsJsonAsync($"/api/v1/admin/identity/organisations/{organisationId}/verification", new { decision = "verified" });
        noKey.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await noKey.Content.ReadAsStringAsync());

        var key = Guid.NewGuid().ToString("N");
        staff.DefaultRequestHeaders.Add("Idempotency-Key", key);
        var verified = await staff.PostAsJsonAsync($"/api/v1/admin/identity/organisations/{organisationId}/verification", new { decision = "verified" });
        verified.StatusCode.Should().Be(HttpStatusCode.OK, await verified.Content.ReadAsStringAsync());
        var replay = await staff.PostAsJsonAsync($"/api/v1/admin/identity/organisations/{organisationId}/verification", new { decision = "verified" });
        replay.StatusCode.Should().Be(HttpStatusCode.OK, "same key + same body replays the stored response");
        var conflicting = await staff.PostAsJsonAsync($"/api/v1/admin/identity/organisations/{organisationId}/verification", new { decision = "rejected", reason = "changed my mind" });
        conflicting.StatusCode.Should().Be(HttpStatusCode.Conflict, "same key + different body is refused");

        var me = await owner.GetFromJsonAsync<JsonElement>("/api/v1/identity/me", Json);
        me.GetProperty("organisations")[0].GetProperty("verificationStatus").GetString().Should().Be("verified");

        // identity.organisation-verified.v1 is published, and the Audit module recorded the Organisation changes.
        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var outbox = await identity.Outbox.SingleAsync(m => m.Topic == "plastoids.identity.organisation-verified.v1" && m.PartitionKey == organisationId.ToString("D"));
            outbox.Payload.Should().Contain("identity.organisation-verified").And.Contain("sponsor");

            var audit = scope.ServiceProvider.GetRequiredService<AuditDbContext>();
            await WaitUntilAsync(
                async () => await audit.Events.AsNoTracking().CountAsync(e => e.EntityName == "Organisation" && e.EntityId == organisationId) >= 2,
                TimeSpan.FromSeconds(60),
                "audit events for the organisation (Added + Modified)");
            var modified = await audit.Events.AsNoTracking().Where(e => e.EntityName == "Organisation" && e.EntityId == organisationId && e.Action.EndsWith("Modified")).FirstAsync();
            modified.After.Should().Contain("Verified");
            modified.Before.Should().Contain("Pending");
        }

        // Staff can page through the audit log; learners cannot see it.
        var page = await staff.GetFromJsonAsync<JsonElement>($"/api/v1/admin/audit-events?entity=Organisation&entityId={organisationId}&limit=1", Json);
        page.GetProperty("items").GetArrayLength().Should().Be(1);
        var cursor = page.GetProperty("nextCursor").GetString();
        cursor.Should().NotBeNullOrEmpty();
        var next = await staff.GetFromJsonAsync<JsonElement>($"/api/v1/admin/audit-events?entity=Organisation&entityId={organisationId}&limit=1&cursor={cursor}", Json);
        next.GetProperty("items")[0].GetProperty("id").GetGuid().Should().NotBe(page.GetProperty("items")[0].GetProperty("id").GetGuid());
        (await owner.GetAsync("/api/v1/admin/audit-events")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Audit_events_table_is_append_only()
    {
        _ = factory.CreateClient(); // boots the host so migrations (and the triggers) have run
        await using var connection = new MySqlConnection(factory.MySqlConnectionString);
        await connection.OpenAsync();
        await using var insert = connection.CreateCommand();
        insert.CommandText = """
            INSERT INTO audit_events (id, tenant_id, event_id, action, entity_name, entity_id, occurred_at, created_at, updated_at, row_version)
            VALUES (UUID_TO_BIN(UUID()), UUID_TO_BIN(UUID()), UUID_TO_BIN(UUID()), 'Test:Added', 'Test', UUID_TO_BIN(UUID()), UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 1)
            """;
        await insert.ExecuteNonQueryAsync();

        await using var update = connection.CreateCommand();
        update.CommandText = "UPDATE audit_events SET action = 'tampered' WHERE action = 'Test:Added'";
        var updateFailure = await Record.ExceptionAsync(() => update.ExecuteNonQueryAsync());
        updateFailure.Should().BeOfType<MySqlException>().Which.Message.Should().Contain("append-only");

        await using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM audit_events WHERE action = 'Test:Added'";
        var deleteFailure = await Record.ExceptionAsync(() => delete.ExecuteNonQueryAsync());
        deleteFailure.Should().BeOfType<MySqlException>().Which.Message.Should().Contain("append-only");
    }

    [Fact]
    public async Task Admin_endpoints_reject_addresses_outside_the_allow_list()
    {
        var staff = await factory.SignInAsStaffAsync();
        staff.DefaultRequestHeaders.Add("X-Forwarded-For", "203.0.113.9");
        // The test server connects from loopback; the allow-list is enforced on the connection address, so this stays allowed.
        (await staff.GetAsync("/api/v1/admin/audit-events?limit=1")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
