using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Certificates.Persistence;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Tenancy;
using static Plastoids.IntegrationTests.ApiClientExtensions;

namespace Plastoids.IntegrationTests;

[Collection(ApiCollection.Name)]
public class LearningAndCertificatesTests(PlastoidsApiFactory factory)
{
    /// <summary>Published course with a document lesson, a quiz (pass mark 70, 2 questions) and a ₦45,000 self-paced offering.</summary>
    internal static async Task<(HttpClient Tutor, Guid TutorUserId, Guid CourseId, string Slug, Guid DocLessonId, Guid QuizLessonId)> CourseWithQuizAsync(PlastoidsApiFactory factory)
    {
        var (tutor, userId, _) = await CatalogueTests.VerifiedTutorAsync(factory);
        var created = await tutor.PostAsJsonAsync("/api/v1/catalogue/courses", new { title = $"Quiz course {Guid.NewGuid():N}", summary = "x", level = "beginner", language = "en", category = "data", skills = new[] { "sql" } });
        var course = await created.Content.ReadFromJsonAsync<JsonElement>(Json);
        var courseId = course.GetProperty("courseId").GetGuid();
        var slug = course.GetProperty("slug").GetString()!;
        var outline = await tutor.PutAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/outline", new { summary = "A complete course with reading material and a graded quiz so learners can earn a verifiable Plastoids certificate at the end.", modules = new[] { new { title = "Module 1", order = 1 } } });
        var moduleId = (await outline.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("modules")[0].GetProperty("id").GetGuid();
        var doc = await tutor.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/modules/{moduleId}/lessons", new { type = "doc", title = "Reading", order = 1, durationMinutes = 10, isPreview = false, contentJson = "{\"pages\":3,\"text\":\"SELECT basics\"}" });
        var docId = (await doc.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("lessonId").GetGuid();
        var quiz = await tutor.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/modules/{moduleId}/quizzes", new { title = "Check", order = 2, passMarkPercent = 70, questions = new object[] { new { prompt = "Keep unmatched left rows?", options = new[] { "INNER", "LEFT" }, correctIndex = 1 }, new { prompt = "Count rows", options = new[] { "COUNT(*)", "SUM" }, correctIndex = 0 } } });
        var quizId = (await quiz.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("lessonId").GetGuid();
        await tutor.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/offerings", new { format = "self_paced", price = new { amount = 4500000, currency = "NGN" }, fundingTypes = new[] { "self" }, capacity = (int?)null, startAt = (DateTime?)null, durationWeeks = 4 });
        var submit = await tutor.PostAsync($"/api/v1/catalogue/courses/{courseId}/submit", null);
        submit.IsSuccessStatusCode.Should().BeTrue(await submit.Content.ReadAsStringAsync());
        if ((await submit.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("requiresHumanReview").GetBoolean())
        {
            var staff = await factory.SignInAsStaffAsync();
            staff.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
            (await staff.PostAsJsonAsync($"/api/v1/admin/catalogue/courses/{courseId}/review", new { decision = "approved" })).IsSuccessStatusCode.Should().BeTrue();
        }

        (await tutor.PostAsync($"/api/v1/catalogue/courses/{courseId}/publish", null)).IsSuccessStatusCode.Should().BeTrue();
        return (tutor, userId, courseId, slug, docId, quizId);
    }

    /// <summary>Buys the course through the fake gateway and waits for the enrolment the payment consumer creates.</summary>
    internal static async Task<Guid> EnrolByPaymentAsync(PlastoidsApiFactory factory, HttpClient learner, string slug)
    {
        var detail = await learner.GetFromJsonAsync<JsonElement>($"/api/v1/catalogue/courses/{slug}", Json);
        var quoteId = detail.GetProperty("offerings")[0].GetProperty("quoteId").GetString()!;
        learner.DefaultRequestHeaders.Remove("Idempotency-Key");
        learner.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var created = await learner.PostAsJsonAsync("/api/v1/payments/intents", new { purpose = "course", quoteId });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var intent = await created.Content.ReadFromJsonAsync<JsonElement>(Json);
        learner.DefaultRequestHeaders.Remove("Idempotency-Key");

        var gateway = factory.Services.GetRequiredService<FakePaymentGateway>();
        var (payload, signature) = gateway.BuildWebhook("charge.success", intent.GetProperty("gatewayReference").GetString()!, Money.NairaAmount(45_000), DateTime.UtcNow);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/payments/webhooks/fake") { Content = new StringContent(payload, Encoding.UTF8, "application/json"), Headers = { { "x-paystack-signature", signature } } };
        (await factory.CreateClient().SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.OK);

        JsonElement enrolments = default;
        await WaitUntilAsync(async () =>
        {
            enrolments = await learner.GetFromJsonAsync<JsonElement>("/api/v1/lms/enrolments/me", Json);
            return enrolments.EnumerateArray().Any(e => e.GetProperty("courseSlug").GetString() == slug);
        }, TimeSpan.FromSeconds(60), "enrolment from the payment consumer");
        return enrolments.EnumerateArray().Single(e => e.GetProperty("courseSlug").GetString() == slug).GetProperty("enrolmentId").GetGuid();
    }

    [Fact]
    public async Task Learner_studies_passes_the_quiz_completes_and_receives_a_verifiable_certificate()
    {
        var (tutor, _, courseId, slug, docId, quizId) = await CourseWithQuizAsync(factory);
        var (learner, learnerSignIn) = await factory.SignInWithPhoneAsync(NewPhone(), "Chiamaka Okafor");
        var enrolmentId = await EnrolByPaymentAsync(factory, learner, slug);

        // Lesson content: document comes through; quiz questions arrive without answers.
        var doc = await learner.GetFromJsonAsync<JsonElement>($"/api/v1/lms/enrolments/{enrolmentId}/lessons/{docId}", Json);
        doc.GetProperty("document").GetProperty("pages").GetInt32().Should().Be(3);
        var quizView = await learner.GetFromJsonAsync<JsonElement>($"/api/v1/lms/enrolments/{enrolmentId}/lessons/{quizId}", Json);
        quizView.GetProperty("questions").GetArrayLength().Should().Be(2);
        quizView.GetProperty("questions")[0].TryGetProperty("correctIndex", out _).Should().BeFalse();

        // Offline batch: a stale record is ignored, completion wins; the quiz cannot be self-completed.
        var t0 = DateTime.UtcNow.AddMinutes(-30);
        var progress = await learner.PostAsJsonAsync($"/api/v1/lms/enrolments/{enrolmentId}/progress", new
        {
            items = new object[]
            {
                new { lessonId = docId, status = "inprogress", positionSeconds = 0, page = 2, percentComplete = 50, clientUpdatedAt = t0.AddMinutes(5) },
                new { lessonId = docId, status = "inprogress", positionSeconds = 0, page = 1, percentComplete = 20, clientUpdatedAt = t0 },
                new { lessonId = docId, status = "completed", positionSeconds = 0, page = 3, percentComplete = 100, clientUpdatedAt = t0.AddMinutes(10) },
                new { lessonId = quizId, status = "completed", positionSeconds = 0, page = 0, percentComplete = 100, clientUpdatedAt = t0.AddMinutes(10) },
            },
        });
        progress.StatusCode.Should().Be(HttpStatusCode.OK, await progress.Content.ReadAsStringAsync());
        var progressBody = await progress.Content.ReadFromJsonAsync<JsonElement>(Json);
        progressBody.GetProperty("applied").GetInt32().Should().Be(2);
        progressBody.GetProperty("ignored").GetInt32().Should().Be(2);
        progressBody.GetProperty("progressPct").GetDecimal().Should().Be(50m);
        progressBody.GetProperty("completed").GetBoolean().Should().BeFalse();

        // Catalogue review gate is now real: 50 % progress on a verified enrolment allows a review.
        (await learner.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/reviews", new { enrolmentId = Guid.NewGuid(), rating = 5, body = "Not my enrolment" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var review = await learner.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/reviews", new { enrolmentId, rating = 5, body = "Clear and practical, finished the reading in a day." });
        review.StatusCode.Should().Be(HttpStatusCode.Created, await review.Content.ReadAsStringAsync());

        // Quiz: start, save, submit → graded and the enrolment completes.
        var started = await learner.PostAsync($"/api/v1/lms/enrolments/{enrolmentId}/lessons/{quizId}/attempts", null);
        started.StatusCode.Should().Be(HttpStatusCode.Created, await started.Content.ReadAsStringAsync());
        var attempt = await started.Content.ReadFromJsonAsync<JsonElement>(Json);
        var attemptId = attempt.GetProperty("attemptId").GetGuid();
        attempt.GetProperty("secondsRemaining").GetInt32().Should().BeInRange(1700, 1800);
        (await learner.PutAsJsonAsync($"/api/v1/lms/attempts/{attemptId}/answers", new { answers = new Dictionary<string, object> { ["0"] = 1 } })).StatusCode.Should().Be(HttpStatusCode.OK);
        var submitted = await learner.PostAsJsonAsync($"/api/v1/lms/attempts/{attemptId}/submit", new { answers = new Dictionary<string, object> { ["0"] = 1, ["1"] = 0 } });
        submitted.StatusCode.Should().Be(HttpStatusCode.OK, await submitted.Content.ReadAsStringAsync());
        var result = await submitted.Content.ReadFromJsonAsync<JsonElement>(Json);
        result.GetProperty("passed").GetBoolean().Should().BeTrue();
        result.GetProperty("score").GetDecimal().Should().Be(100m);
        result.GetProperty("enrolmentCompleted").GetBoolean().Should().BeTrue();
        (await learner.PostAsJsonAsync($"/api/v1/lms/attempts/{attemptId}/submit", new { })).StatusCode.Should().Be(HttpStatusCode.Conflict, "an attempt submits once");

        var gradebook = await learner.GetFromJsonAsync<JsonElement>($"/api/v1/lms/enrolments/{enrolmentId}/gradebook", Json);
        gradebook.GetProperty("rows")[0].GetProperty("status").GetString().Should().Be("completed");
        gradebook.GetProperty("rows")[0].GetProperty("finalScore").GetDecimal().Should().Be(100m);
        var tutorBook = await tutor.GetFromJsonAsync<JsonElement>($"/api/v1/lms/courses/{courseId}/gradebook", Json);
        tutorBook.GetProperty("rows").GetArrayLength().Should().BeGreaterThanOrEqualTo(1);

        // Certificate: issued by the consumer, listed, verifiable, revocable, immutable.
        JsonElement certificates = default;
        await WaitUntilAsync(async () =>
        {
            certificates = await learner.GetFromJsonAsync<JsonElement>("/api/v1/certificates/me", Json);
            return certificates.EnumerateArray().Any(c => c.GetProperty("enrolmentId").GetGuid() == enrolmentId);
        }, TimeSpan.FromSeconds(60), "certificate issuance");
        var certificate = certificates.EnumerateArray().Single(c => c.GetProperty("enrolmentId").GetGuid() == enrolmentId);
        var publicCode = certificate.GetProperty("publicCode").GetString()!;
        certificate.GetProperty("learnerName").GetString().Should().Be("Chiamaka Okafor");

        var verify = await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/verify/{publicCode}", Json);
        verify.GetProperty("status").GetString().Should().Be("valid");
        verify.GetProperty("hashMatches").GetBoolean().Should().BeTrue();
        (await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/v1/verify/AAAA-BBBB-CCCC", Json)).GetProperty("status").GetString().Should().Be("unknown");

        var staff = await factory.SignInAsStaffAsync();
        staff.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString("N"));
        var revoked = await staff.PostAsJsonAsync($"/api/v1/admin/certificates/{certificate.GetProperty("certificateId").GetGuid()}/revoke", new { reason = "Issued in error during testing" });
        revoked.StatusCode.Should().Be(HttpStatusCode.OK, await revoked.Content.ReadAsStringAsync());
        (await factory.CreateClient().GetFromJsonAsync<JsonElement>($"/api/v1/verify/{publicCode}", Json)).GetProperty("status").GetString().Should().Be("revoked");

        await using (var connection = new MySqlConnection(Plastoids.Infrastructure.InfrastructureServiceCollectionExtensions.EnsureGuidFormat(factory.MySqlConnectionString)))
        {
            await connection.OpenAsync();
            await using var tamper = connection.CreateCommand();
            tamper.CommandText = "UPDATE certificates_certificates SET final_score = 99 WHERE public_code = @code";
            tamper.Parameters.AddWithValue("@code", publicCode);
            var failure = await Record.ExceptionAsync(() => tamper.ExecuteNonQueryAsync());
            failure.Should().BeOfType<MySqlException>().Which.Message.Should().Contain("immutable");
        }

        using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var lms = scope.ServiceProvider.GetRequiredService<LmsDbContext>();
            (await lms.Outbox.AsNoTracking().CountAsync(m => m.Topic == "plastoids.lms.enrolment-completed.v1" && m.PartitionKey == enrolmentId.ToString("D"))).Should().Be(1);
            (await lms.Outbox.AsNoTracking().CountAsync(m => m.Topic == "plastoids.lms.attempt-graded.v1")).Should().BeGreaterThanOrEqualTo(1);
            var certs = scope.ServiceProvider.GetRequiredService<CertificatesDbContext>();
            (await certs.Verifications.AsNoTracking().CountAsync(v => v.PublicCode == publicCode)).Should().Be(2);
        }

        // Offline package tied to the enrolment.
        var offline = await learner.PostAsync($"/api/v1/lms/enrolments/{enrolmentId}/offline-package", null);
        offline.StatusCode.Should().Be(HttpStatusCode.Conflict, "the enrolment is completed, not active");
        learnerSignIn.UserId.Should().NotBeEmpty();
        tutor.Dispose();
    }

    [Fact]
    public async Task Cohort_sessions_and_forum_moderation()
    {
        var (tutor, _, courseId, _, _, _) = await CourseWithQuizAsync(factory);
        var cohortOffering = await tutor.PostAsJsonAsync($"/api/v1/catalogue/courses/{courseId}/offerings", new { format = "cohort", price = new { amount = 10000000, currency = "NGN" }, fundingTypes = new[] { "sponsored" }, capacity = 30, startAt = DateTime.UtcNow.AddDays(10), durationWeeks = 8 });
        cohortOffering.StatusCode.Should().Be(HttpStatusCode.Created, await cohortOffering.Content.ReadAsStringAsync());
        var offeringId = (await cohortOffering.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("offeringId").GetGuid();

        var created = await tutor.PostAsJsonAsync("/api/v1/lms/cohorts", new { courseId, offeringId, name = "January intake", startsAt = DateTime.UtcNow.AddDays(10), endsAt = DateTime.UtcNow.AddDays(24), capacity = 25, programmeId = (Guid?)null });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var cohortId = (await created.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("cohortId").GetGuid();

        var scheduled = await tutor.PostAsJsonAsync($"/api/v1/lms/cohorts/{cohortId}/sessions", new { weekdays = new[] { "monday", "wednesday" }, startTimeUtc = "18:00", durationMinutes = 90, titlePrefix = "Live class" });
        scheduled.StatusCode.Should().Be(HttpStatusCode.Created, await scheduled.Content.ReadAsStringAsync());
        (await scheduled.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("created").GetInt32().Should().Be(4);
        var again = await tutor.PostAsJsonAsync($"/api/v1/lms/cohorts/{cohortId}/sessions", new { weekdays = new[] { "monday" }, startTimeUtc = "18:00", durationMinutes = 90 });
        (await again.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("created").GetInt32().Should().Be(0, "idempotent on start time");

        var thread = await tutor.PostAsJsonAsync($"/api/v1/lms/cohorts/{cohortId}/threads", new { title = "Welcome", body = "Introduce yourselves here." });
        thread.StatusCode.Should().Be(HttpStatusCode.Created, await thread.Content.ReadAsStringAsync());
        var threadId = (await thread.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("threadId").GetGuid();
        var flagged = await tutor.PostAsJsonAsync($"/api/v1/lms/threads/{threadId}/posts", new { body = "You can pay me directly for extra classes, call 08031234567" });
        var post = await flagged.Content.ReadFromJsonAsync<JsonElement>(Json);
        post.GetProperty("flagged").GetBoolean().Should().BeTrue();
        var threads = await tutor.GetFromJsonAsync<JsonElement>($"/api/v1/lms/cohorts/{cohortId}/threads", Json);
        threads[0].GetProperty("status").GetString().Should().Be("flagged");

        var (outsider, _) = await factory.SignInWithPhoneAsync(NewPhone(), "Outsider");
        (await outsider.GetAsync($"/api/v1/lms/cohorts/{cohortId}/threads")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        tutor.Dispose();
    }
}
