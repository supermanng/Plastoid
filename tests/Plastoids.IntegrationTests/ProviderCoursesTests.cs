using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Plastoids.Infrastructure.Security;
using static Plastoids.IntegrationTests.ApiClientExtensions;

namespace Plastoids.IntegrationTests;

/// <summary>ADR 0019: a verified organisation authors a course, engages a verified tutor on revenue share, sells a cohort the tutor teaches.</summary>
[Collection(ApiCollection.Name)]
public class ProviderCoursesTests(PlastoidsApiFactory factory)
{
    /// <summary>Enrols the client in TOTP MFA and swaps in the amr=mfa token the authoring endpoints (Policies.Mfa) need.</summary>
    internal static async Task EnableMfaAsync(HttpClient client)
    {
        var enable = await (await client.PostAsync("/api/v1/identity/me/mfa/enable", null)).Content.ReadFromJsonAsync<JsonElement>(Json);
        var code = Totp.Compute(Base32.Decode(enable.GetProperty("secret").GetString()!), DateTime.UtcNow);
        var confirm = await client.PostAsJsonAsync("/api/v1/identity/auth/mfa/verify", new { code });
        confirm.IsSuccessStatusCode.Should().BeTrue(await confirm.Content.ReadAsStringAsync());
        client.UseBearer((await confirm.Content.ReadFromJsonAsync<Tokens>(Json))!.AccessToken!);
    }

    [Fact]
    public async Task Organisation_authors_a_course_engages_a_tutor_and_the_tutor_opens_a_cohort()
    {
        var organisationName = "Lagos Data School " + Guid.NewGuid().ToString("N")[..6];
        var (owner, organisationId, _) = await SponsorshipAndApplicationsTests.VerifiedSponsorAsync(factory, organisationName);
        await EnableMfaAsync(owner);
        var (tutor, tutorUserId, tutorId) = await CatalogueTests.VerifiedTutorAsync(factory);

        // The organisation drafts a course through the organisation route; a stranger with an MFA session cannot read it.
        var created = await owner.PostAsJsonAsync($"/api/v1/catalogue/organisations/{organisationId}/courses", new { title = "Analytics Engineering Bootcamp", summary = "Short.", level = "intermediate", language = "en", category = "data", skills = new[] { "dbt", "sql" } });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var course = await created.Content.ReadFromJsonAsync<JsonElement>(Json);
        var courseId = course.GetProperty("courseId").GetGuid();
        var slug = course.GetProperty("slug").GetString()!;
        course.GetProperty("status").GetString().Should().Be("draft");

        var outline = await owner.PutAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/outline", new
        {
            summary = "Twelve weeks of hands-on analytics engineering: modelling with dbt, warehouse design, testing and orchestration on Nigerian fintech datasets.",
            modules = new[] { new { title = "Modelling", order = 1 }, new { title = "Testing", order = 2 } },
        });
        outline.StatusCode.Should().Be(HttpStatusCode.OK, await outline.Content.ReadAsStringAsync());
        var moduleIds = (await outline.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("modules").EnumerateArray().Select(m => m.GetProperty("id").GetGuid()).ToList();
        var lesson = await owner.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/modules/{moduleIds[0]}/lessons", new { type = "doc", title = "Why model?", order = 1, durationMinutes = 15, isPreview = true, contentJson = (string?)null });
        lesson.StatusCode.Should().Be(HttpStatusCode.Created, await lesson.Content.ReadAsStringAsync());
        var quiz = await owner.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/modules/{moduleIds[1]}/quizzes", new { title = "Testing check", order = 1, passMarkPercent = 70, questions = new[] { new { prompt = "Which dbt test checks uniqueness?", options = new[] { "unique", "not_null" }, correctIndex = 0 } } });
        quiz.StatusCode.Should().Be(HttpStatusCode.Created, await quiz.Content.ReadAsStringAsync());

        var (stranger, _, _) = await TutorOnboardingTests.ApplyAsTutorAsync(factory, "Stranger");
        var strangerView = await stranger.GetAsync($"/api/v1/catalogue/courses/{courseId}/authoring");
        strangerView.StatusCode.Should().Be(HttpStatusCode.Forbidden, await strangerView.Content.ReadAsStringAsync());
        (await strangerView.Content.ReadAsStringAsync()).Should().Contain("not_course_owner");

        // A cohort offering needs an engaged tutor; the tutor is not engaged yet.
        var engagementsUrl = $"/api/v1/catalogue/organisations/{organisationId}/courses/{courseId}/engagements";
        var startAt = DateTime.UtcNow.AddDays(14);
        var offeringBody = new { format = "cohort", price = new { amount = 4500000, currency = "NGN" }, fundingTypes = new[] { "self", "sponsored" }, capacity = 20, startAt, durationWeeks = 12, tutorUserId = (Guid?)null };
        var noTutor = await owner.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/offerings", offeringBody);
        noTutor.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await noTutor.Content.ReadAsStringAsync());
        (await noTutor.Content.ReadAsStringAsync()).Should().Contain("tutor_required");
        var notEngaged = await owner.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/offerings", offeringBody with { tutorUserId = tutorUserId });
        notEngaged.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await notEngaged.Content.ReadAsStringAsync());
        (await notEngaged.Content.ReadAsStringAsync()).Should().Contain("tutor_not_engaged");

        // Offer the tutor 40 % of the net; a second open offer to the same tutor conflicts; the tutor sees and accepts it.
        var offered = await owner.PostAsJsonAsync(engagementsUrl, new { tutorId, terms = new { kind = "revenue_share", shareBps = 4000 }, note = "Teach the January cohort" });
        offered.StatusCode.Should().Be(HttpStatusCode.Created, await offered.Content.ReadAsStringAsync());
        var engagement = await offered.Content.ReadFromJsonAsync<JsonElement>(Json);
        var engagementId = engagement.GetProperty("engagementId").GetGuid();
        engagement.GetProperty("status").GetString().Should().Be("offered");
        engagement.GetProperty("organisationName").GetString().Should().Be(organisationName);
        engagement.GetProperty("tutorUserId").GetGuid().Should().Be(tutorUserId);
        engagement.GetProperty("terms").GetProperty("shareBps").GetInt32().Should().Be(4000);
        (await owner.PostAsJsonAsync(engagementsUrl, new { tutorId, terms = new { kind = "revenue_share", shareBps = 3000 } })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        var mine = await tutor.GetFromJsonAsync<JsonElement>("/api/v1/catalogue/engagements/mine", Json);
        mine.EnumerateArray().Single(e => e.GetProperty("engagementId").GetGuid() == engagementId).GetProperty("status").GetString().Should().Be("offered");
        (await stranger.PostAsJsonAsync($"/api/v1/catalogue/engagements/{engagementId}/respond", new { accept = true })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var accepted = await tutor.PostAsJsonAsync($"/api/v1/catalogue/engagements/{engagementId}/respond", new { accept = true, note = "Happy to" });
        accepted.StatusCode.Should().Be(HttpStatusCode.OK, await accepted.Content.ReadAsStringAsync());
        (await accepted.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("status").GetString().Should().Be("active");

        // Now the cohort offering can name the tutor; submit → staff review → publish by the organisation.
        var offering = await owner.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/offerings", offeringBody with { tutorUserId = tutorUserId });
        offering.StatusCode.Should().Be(HttpStatusCode.Created, await offering.Content.ReadAsStringAsync());
        var offeringBodyJson = await offering.Content.ReadFromJsonAsync<JsonElement>(Json);
        var offeringId = offeringBodyJson.GetProperty("offeringId").GetGuid();
        offeringBodyJson.GetProperty("engagementId").GetGuid().Should().Be(engagementId);

        var submit = await owner.PostAsync($"/api/v1/catalogue/courses/{courseId}/submit", null);
        submit.StatusCode.Should().Be(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());
        if ((await submit.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("requiresHumanReview").GetBoolean())
        {
            var staff = await factory.SignInAsStaffAsync();
            staff.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            var approved = await staff.PostAsJsonAsync($"/api/v1/admin/catalogue/courses/{courseId}/review", new { decision = "approved" });
            approved.StatusCode.Should().Be(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());
        }

        var published = await owner.PostAsync($"/api/v1/catalogue/courses/{courseId}/publish", null);
        published.StatusCode.Should().Be(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());

        // Authoring view and organisation list for the owner; public detail and search show the provider.
        var authoring = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/catalogue/courses/{courseId}/authoring", Json);
        authoring.GetProperty("ownerType").GetString().Should().Be("organisation");
        authoring.GetProperty("organisationId").GetGuid().Should().Be(organisationId);
        authoring.GetProperty("status").GetString().Should().Be("published");
        authoring.GetProperty("modules").GetArrayLength().Should().Be(2);
        authoring.GetProperty("modules")[1].GetProperty("lessons")[0].GetProperty("contentJson").GetString().Should().Contain("passMarkPercent");
        authoring.GetProperty("offerings")[0].GetProperty("tutorUserId").GetGuid().Should().Be(tutorUserId);

        var list = await owner.GetFromJsonAsync<JsonElement>($"/api/v1/catalogue/organisations/{organisationId}/courses", Json);
        var summary = list.EnumerateArray().Single(c => c.GetProperty("courseId").GetGuid() == courseId);
        summary.GetProperty("activeEngagements").GetInt32().Should().Be(1);
        summary.GetProperty("offeringCount").GetInt32().Should().Be(1);
        summary.GetProperty("currentVersionNo").GetInt32().Should().Be(1);

        var courseEngagements = await owner.GetFromJsonAsync<JsonElement>(engagementsUrl, Json);
        courseEngagements.EnumerateArray().Single().GetProperty("status").GetString().Should().Be("active");

        var anonymous = factory.CreateClient();
        var detail = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/catalogue/courses/{slug}", Json);
        detail.GetProperty("ownerType").GetString().Should().Be("organisation");
        detail.GetProperty("provider").GetProperty("organisationId").GetGuid().Should().Be(organisationId);
        detail.GetProperty("provider").GetProperty("name").GetString().Should().Be(organisationName);
        detail.GetProperty("offerings")[0].GetProperty("offeringId").GetGuid().Should().Be(offeringId);
        var search = await anonymous.GetFromJsonAsync<JsonElement>("/api/v1/catalogue/courses?skill=dbt", Json);
        search.GetProperty("items").EnumerateArray().Single(c => c.GetProperty("courseId").GetGuid() == courseId).GetProperty("providerName").GetString().Should().Be(organisationName);

        var active = await tutor.GetFromJsonAsync<JsonElement>("/api/v1/catalogue/engagements/mine", Json);
        var activeRow = active.EnumerateArray().Single(e => e.GetProperty("engagementId").GetGuid() == engagementId);
        activeRow.GetProperty("status").GetString().Should().Be("active");
        activeRow.GetProperty("courseSlug").GetString().Should().Be(slug);

        // The engaged tutor opens a cohort on the offering (defaults to themselves); a stranger may not; the organisation may name the tutor.
        var cohort = await tutor.PostAsJsonAsync("/api/v1/lms/cohorts", new { courseId, offeringId, name = "January cohort", startsAt = startAt, endsAt = startAt.AddDays(84), capacity = 20, programmeId = (Guid?)null });
        cohort.StatusCode.Should().Be(HttpStatusCode.Created, await cohort.Content.ReadAsStringAsync());
        (await cohort.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("tutorUserId").GetGuid().Should().Be(tutorUserId);
        var strangerCohort = await stranger.PostAsJsonAsync("/api/v1/lms/cohorts", new { courseId, offeringId, name = "Hijacked", startsAt = startAt, endsAt = startAt.AddDays(84), capacity = 20, programmeId = (Guid?)null });
        strangerCohort.StatusCode.Should().Be(HttpStatusCode.Forbidden, await strangerCohort.Content.ReadAsStringAsync());
        var ownerCohort = await owner.PostAsJsonAsync("/api/v1/lms/cohorts", new { courseId, offeringId, name = "April cohort", startsAt = startAt.AddDays(90), endsAt = startAt.AddDays(174), capacity = 20, programmeId = (Guid?)null, tutorUserId });
        ownerCohort.StatusCode.Should().Be(HttpStatusCode.Created, await ownerCohort.Content.ReadAsStringAsync());

        // Ending the engagement is the organisation's call, not the tutor's; afterwards the tutor can no longer open cohorts.
        (await tutor.PostAsJsonAsync($"/api/v1/catalogue/engagements/{engagementId}/end", new { reason = "nope" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var ended = await owner.PostAsJsonAsync($"/api/v1/catalogue/engagements/{engagementId}/end", new { reason = "Season over" });
        ended.StatusCode.Should().Be(HttpStatusCode.OK, await ended.Content.ReadAsStringAsync());
        (await ended.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("status").GetString().Should().Be("ended");
        var afterEnd = await tutor.PostAsJsonAsync("/api/v1/lms/cohorts", new { courseId, offeringId, name = "Too late", startsAt = startAt, endsAt = startAt.AddDays(84), capacity = 20, programmeId = (Guid?)null });
        afterEnd.StatusCode.Should().Be(HttpStatusCode.Forbidden, await afterEnd.Content.ReadAsStringAsync());
    }
}
