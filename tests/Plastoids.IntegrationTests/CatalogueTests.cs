using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Tenancy;
using static Plastoids.IntegrationTests.ApiClientExtensions;

namespace Plastoids.IntegrationTests;

[Collection(ApiCollection.Name)]
public class CatalogueTests(PlastoidsApiFactory factory)
{
    /// <summary>A verified tutor with one published, sponsorable self-paced course (₦45,000). Used by Payments tests.</summary>
    internal static async Task<(HttpClient Tutor, Guid TutorUserId, string Slug, Guid CourseId)> PublishedCourseAsync(PlastoidsApiFactory factory)
    {
        var (tutor, userId, _) = await VerifiedTutorAsync(factory);
        var created = await tutor.PostAsJsonAsync("/api/v1/catalogue/courses", new { title = $"Paid course {Guid.NewGuid():N}", summary = "x", level = "beginner", language = "en", category = "data", skills = new[] { "sql" } });
        var course = await created.Content.ReadFromJsonAsync<JsonElement>(Json);
        var courseId = course.GetProperty("courseId").GetGuid();
        var slug = course.GetProperty("slug").GetString()!;
        var outline = await tutor.PutAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/outline", new { summary = "A course long enough to pass the automated summary check with plenty of practical content for learners who want to get started.", modules = new[] { new { title = "Intro", order = 1 } } });
        var moduleId = (await outline.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("modules")[0].GetProperty("id").GetGuid();
        await tutor.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/modules/{moduleId}/lessons", new { type = "doc", title = "Read me", order = 1, durationMinutes = 5, isPreview = false, contentJson = (string?)null });
        await tutor.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/offerings", new { format = "self_paced", price = new { amount = 4500000, currency = "NGN" }, fundingTypes = new[] { "self", "sponsored" }, capacity = (int?)null, startAt = (DateTime?)null, durationWeeks = 4 });
        var submit = await tutor.PostAsync($"/api/v1/catalogue/courses/{courseId}/submit", null);
        submit.IsSuccessStatusCode.Should().BeTrue(await submit.Content.ReadAsStringAsync());
        if ((await submit.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("requiresHumanReview").GetBoolean())
        {
            var staff = await factory.SignInAsStaffAsync();
            staff.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            (await staff.PostAsJsonAsync($"/api/v1/admin/catalogue/courses/{courseId}/review", new { decision = "approved" })).IsSuccessStatusCode.Should().BeTrue();
        }

        var published = await tutor.PostAsync($"/api/v1/catalogue/courses/{courseId}/publish", null);
        published.IsSuccessStatusCode.Should().BeTrue(await published.Content.ReadAsStringAsync());
        return (tutor, userId, slug, courseId);
    }

    private static async Task<(HttpClient Tutor, Guid UserId, Guid TutorId)> VerifiedTutorAsync(PlastoidsApiFactory factory)
    {
        var (tutor, userId, tutorId) = await TutorOnboardingTests.ApplyAsTutorAsync(factory, "Course author");
        await tutor.PostAsJsonAsync("/api/v1/tutors/me/identity-check", new { nin = "12345678901", bvn = "22345678901", selfieBlobKey = "s.jpg", fullName = "Course Author" });
        var credentialId = (await (await tutor.PostAsJsonAsync("/api/v1/tutors/me/credentials", new { type = "certificate", issuer = "AWS", title = "SAA", contentType = "application/pdf" })).Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("credentialId").GetGuid();
        await tutor.PostAsJsonAsync("/api/v1/tutors/me/demo-lesson", new { contentType = "video/mp4" });
        var staff = await factory.SignInAsStaffAsync();
        staff.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        (await staff.PostAsJsonAsync($"/api/v1/admin/tutors/credentials/{credentialId}/review", new { decision = "approved" })).IsSuccessStatusCode.Should().BeTrue();
        staff.DefaultRequestHeaders.Remove("Idempotency-Key");
        staff.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        (await staff.PostAsJsonAsync($"/api/v1/admin/tutors/{tutorId}/subject-review", new { decision = "approved" })).IsSuccessStatusCode.Should().BeTrue();

        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var catalogue = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            await WaitUntilAsync(async () => await catalogue.VerifiedTutors.AsNoTracking().AnyAsync(t => t.TutorId == tutorId), TimeSpan.FromSeconds(60), "catalogue projection of the verified tutor");
        }

        return (tutor, userId, tutorId);
    }

    [Fact]
    public async Task Course_authoring_review_publish_search_compare_and_review_gate()
    {
        var (tutor, userId, _) = await VerifiedTutorAsync(factory);
        var unique = Guid.NewGuid().ToString("N")[..6];

        // Create + outline + lessons + quiz + video + offering.
        var created = await tutor.PostAsJsonAsync("/api/v1/catalogue/courses", new { title = $"SQL for Analysts {unique}", summary = "Short.", level = "beginner", language = "en", category = "data", skills = new[] { "SQL", "analytics" } });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var course = await created.Content.ReadFromJsonAsync<JsonElement>(Json);
        var courseId = course.GetProperty("courseId").GetGuid();
        var slug = course.GetProperty("slug").GetString()!;

        var outline = await tutor.PutAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/outline", new
        {
            summary = "Learn to query, clean and present data with SQL through eight hands-on projects drawn from Nigerian fintech datasets. Suitable for complete beginners.",
            modules = new[] { new { title = "Foundations", order = 1 }, new { title = "Joins and aggregation", order = 2 } },
        });
        outline.StatusCode.Should().Be(HttpStatusCode.OK, await outline.Content.ReadAsStringAsync());
        var moduleIds = (await outline.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("modules").EnumerateArray().Select(m => m.GetProperty("id").GetGuid()).ToList();

        var lesson = await tutor.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/modules/{moduleIds[0]}/lessons", new { type = "video", title = "What is a database?", order = 1, durationMinutes = 12, isPreview = true, contentJson = (string?)null });
        lesson.StatusCode.Should().Be(HttpStatusCode.Created, await lesson.Content.ReadAsStringAsync());
        var lessonId = (await lesson.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("lessonId").GetGuid();
        var quiz = await tutor.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/modules/{moduleIds[1]}/quizzes", new { title = "Joins check", order = 1, passMarkPercent = 70, questions = new[] { new { prompt = "Which join keeps unmatched left rows?", options = new[] { "INNER", "LEFT", "CROSS" }, correctIndex = 1 } } });
        quiz.StatusCode.Should().Be(HttpStatusCode.Created, await quiz.Content.ReadAsStringAsync());

        var upload = await tutor.PostAsync($"/api/v1/catalogue/lessons/{lessonId}/video", null);
        upload.StatusCode.Should().Be(HttpStatusCode.Created, await upload.Content.ReadAsStringAsync());
        var uploadBody = await upload.Content.ReadFromJsonAsync<JsonElement>(Json);
        var uploadId = uploadBody.GetProperty("uploadId").GetString()!;
        uploadBody.GetProperty("uploadUrl").GetString().Should().Contain("video-uploads/");

        var offering = await tutor.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/offerings", new { format = "self_paced", price = new { amount = 4500000, currency = "NGN" }, fundingTypes = new[] { "self", "sponsored" }, capacity = (int?)null, startAt = (DateTime?)null, durationWeeks = 6 });
        offering.StatusCode.Should().Be(HttpStatusCode.Created, await offering.Content.ReadAsStringAsync());
        var offeringId = (await offering.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("offeringId").GetGuid();

        // Submit fails while the video is not transcoded (automated check).
        var early = await tutor.PostAsync($"/api/v1/catalogue/courses/{courseId}/submit", null);
        early.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await early.Content.ReadAsStringAsync()).Should().Contain("videos_ready");

        // Provider webhook (signed) marks the asset ready; an unsigned call is rejected.
        var video = factory.Services.GetRequiredService<FakeVideoProvider>();
        var payload = JsonSerializer.Serialize(new { uploadId, status = "ready", playbackId = "pb_" + uploadId, durationSeconds = 720, renditions = "240p,480p,720p" });
        var anonymous = factory.CreateClient();
        var unsigned = await anonymous.PostAsync("/api/v1/catalogue/webhooks/video/fake", new StringContent(payload, Encoding.UTF8, "application/json"));
        unsigned.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var signed = new HttpRequestMessage(HttpMethod.Post, "/api/v1/catalogue/webhooks/video/fake") { Content = new StringContent(payload, Encoding.UTF8, "application/json") };
        signed.Headers.Add("X-Video-Signature", video.Sign(payload));
        (await anonymous.SendAsync(signed)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // First course of this owner → human review required; publish blocked until staff approves.
        var submit = await tutor.PostAsync($"/api/v1/catalogue/courses/{courseId}/submit", null);
        submit.StatusCode.Should().Be(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());
        var submitBody = await submit.Content.ReadFromJsonAsync<JsonElement>(Json);
        submitBody.GetProperty("requiresHumanReview").GetBoolean().Should().BeTrue();
        submitBody.GetProperty("status").GetString().Should().Be("inreview");
        (await tutor.PostAsync($"/api/v1/catalogue/courses/{courseId}/publish", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await factory.CreateClient().GetAsync($"/api/v1/catalogue/courses/{slug}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var staff = await factory.SignInAsStaffAsync();
        staff.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var approved = await staff.PostAsJsonAsync($"/api/v1/admin/catalogue/courses/{courseId}/review", new { decision = "approved" });
        approved.StatusCode.Should().Be(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync());

        var published = await tutor.PostAsync($"/api/v1/catalogue/courses/{courseId}/publish", null);
        published.StatusCode.Should().Be(HttpStatusCode.OK, await published.Content.ReadAsStringAsync());
        (await published.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("versionNo").GetInt32().Should().Be(1);

        // Search (full-text + sponsored filter), detail with quote, compare.
        var search = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/catalogue/courses?q=SQL&funding=sponsored&skill=sql&maxPrice=5000000", Json);
        search.GetProperty("items").EnumerateArray().Select(c => c.GetProperty("courseId").GetGuid()).Should().Contain(courseId);
        var none = await anonymous.GetFromJsonAsync<JsonElement>("/api/v1/catalogue/courses?skill=quantum-knitting", Json);
        none.GetProperty("items").GetArrayLength().Should().Be(0);
        none.GetProperty("suggestions").GetProperty("skills").EnumerateArray().Select(s => s.GetString()).Should().Contain("sql");

        var detail = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/catalogue/courses/{slug}", Json);
        detail.GetProperty("outline").GetArrayLength().Should().Be(2);
        var previewUrl = detail.GetProperty("outline")[0].GetProperty("lessons")[0].GetProperty("previewPlaybackUrl").GetString();
        previewUrl.Should().Contain("pb_" + uploadId);
        var quoteId = detail.GetProperty("offerings")[0].GetProperty("quoteId").GetString()!;
        var quote = await factory.Services.GetRequiredService<PriceQuotes>().GetAsync(quoteId);
        quote!.Price.AmountKobo.Should().Be(4500000);
        quote.OfferingId.Should().Be(offeringId);

        var compare = await anonymous.GetFromJsonAsync<JsonElement>($"/api/v1/catalogue/courses/compare?ids={courseId},{Guid.NewGuid()}", Json);
        compare.GetProperty("courses").GetArrayLength().Should().Be(1);
        compare.GetProperty("courses")[0].GetProperty("lessonCount").GetInt32().Should().Be(2);

        // Review gate: needs a verified enrolment with ≥30% progress (Lms stub).
        var (learner, learnerSignIn) = await factory.SignInWithPhoneAsync(NewPhone(), "Reviewer");
        var enrolmentId = Guid.CreateVersion7();
        var gate = factory.Services.GetRequiredService<StubEnrolmentProgress>();
        (await learner.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/reviews", new { enrolmentId, rating = 5, body = "Clear and practical." })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        gate.Snapshots[enrolmentId] = new EnrolmentProgressSnapshot(enrolmentId, learnerSignIn.UserId, courseId, 10m, true);
        (await learner.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/reviews", new { enrolmentId, rating = 5, body = "Clear and practical." })).StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        gate.Snapshots[enrolmentId] = new EnrolmentProgressSnapshot(enrolmentId, learnerSignIn.UserId, courseId, 45m, true);
        var review = await learner.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/reviews", new { enrolmentId, rating = 5, body = "Clear and practical." });
        review.StatusCode.Should().Be(HttpStatusCode.Created, await review.Content.ReadAsStringAsync());
        (await review.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("courseRatingAverage").GetDecimal().Should().Be(5m);

        // catalogue.course-published.v1 was written to the outbox with the offering.
        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogueDbContext>();
            var row = await db.Outbox.AsNoTracking().SingleAsync(m => m.Topic == "plastoids.catalogue.course-published.v1" && m.PartitionKey == courseId.ToString("D"));
            row.Payload.Should().Contain(offeringId.ToString("D"));
            (await db.Versions.CountAsync(v => v.CourseId == courseId)).Should().Be(1);
        }

        userId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Unverified_tutor_cannot_submit_a_course()
    {
        var (tutor, _, _) = await TutorOnboardingTests.ApplyAsTutorAsync(factory, "Unverified");
        var created = await tutor.PostAsJsonAsync("/api/v1/catalogue/courses", new { title = "Not yet allowed course", summary = "x", level = "beginner", language = "en", category = "data", skills = new[] { "x" } });
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var courseId = (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("courseId").GetGuid();
        var submit = await tutor.PostAsync($"/api/v1/catalogue/courses/{courseId}/submit", null);
        submit.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await submit.Content.ReadAsStringAsync()).Should().Contain("tutor_not_verified");
    }
}
