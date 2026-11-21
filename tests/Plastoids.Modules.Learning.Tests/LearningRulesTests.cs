using System.Text.Json;
using Plastoids.Modules.Lms.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Learning.Tests;

public class LearningRulesTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Grader_scores_single_multi_and_short_answers()
    {
        var quiz = Json("""{"passMarkPercent":70,"questions":[{"prompt":"a","options":["x","y"],"correctIndex":1},{"prompt":"b","options":["x","y","z"],"correctIndexes":[0,2]},{"prompt":"c","answer":"Lagos"}]}""");
        var perfect = Grader.Grade(quiz, Json("""{"0":1,"1":[2,0],"2":" lagos "}"""));
        perfect.Should().Be(new GradeResult(100m, 100m, true, 3, 3));
        var partial = Grader.Grade(quiz, Json("""{"0":1,"1":[0],"2":"Abuja"}"""));
        partial.Score.Should().Be(33.33m);
        partial.Passed.Should().BeFalse();
        Grader.Grade(quiz, Json("{}")).Correct.Should().Be(0);
        Grader.Grade(Json("""{"questions":[]}"""), Json("{}")).Should().Be(new GradeResult(0, 100m, false, 0, 0));
    }

    [Fact]
    public void Attempt_timing_is_enforced_server_side()
    {
        var attempt = Attempt.Start(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), 1, 30, Now);
        attempt.DeadlineAt.Should().Be(Now.AddMinutes(30));
        attempt.SaveAnswers("""{"0":1}""", Now.AddMinutes(10)).IsSuccess.Should().BeTrue();
        attempt.SaveAnswers("""{"0":0}""", Now.AddMinutes(31)).Error.Code.Should().Be("attempt_expired");
        attempt.Status.Should().Be(AttemptStatus.Expired);
        attempt.AnswersJson.Should().Be("""{"0":1}""", "late answers are discarded");
        attempt.Submit(new GradeResult(100, 100, true, 1, 1), Now.AddMinutes(32)).Error.Code.Should().Be("attempt_closed");

        var late = Attempt.Start(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), 1, 30, Now);
        late.Submit(new GradeResult(100, 100, true, 1, 1), Now.AddMinutes(31)).IsSuccess.Should().BeTrue();
        late.Status.Should().Be(AttemptStatus.Expired);
        late.Passed.Should().BeFalse("a submission after the grace period never passes");

        var onTime = Attempt.Start(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), 1, 30, Now);
        onTime.Submit(new GradeResult(80, 100, true, 4, 5), Now.AddMinutes(29)).IsSuccess.Should().BeTrue();
        onTime.Passed.Should().BeTrue();
    }

    [Fact]
    public void Offline_progress_merge_keeps_newest_and_never_regresses_completion()
    {
        var p = LessonProgress.Start(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), Now);
        p.Merge(LessonStatus.InProgress, 120, 0, 40m, Now.AddMinutes(-10), Now).Should().BeTrue();
        p.Merge(LessonStatus.InProgress, 60, 0, 20m, Now.AddMinutes(-20), Now).Should().BeFalse("older offline record");
        p.PositionSeconds.Should().Be(120);
        p.Merge(LessonStatus.Completed, 600, 0, 100m, Now.AddMinutes(-15), Now).Should().BeTrue("completion applies even from an older timestamp");
        p.Status.Should().Be(LessonStatus.Completed);
        p.Merge(LessonStatus.InProgress, 10, 0, 5m, Now, Now).Should().BeTrue();
        p.Status.Should().Be(LessonStatus.Completed, "completion is sticky");
        p.PercentComplete.Should().Be(100m);
    }

    [Fact]
    public void Enrolment_completion_needs_all_required_lessons_and_pass_marks()
    {
        var e = Enrolment.Create(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, 1, "self", Guid.NewGuid(), "payment:x", 3, Now, Now.AddDays(365));
        e.UpdateProgress(2);
        e.ProgressPct.Should().Be(66.67m);
        e.TryComplete(true, 90, Now).Should().BeFalse();
        e.UpdateProgress(3);
        e.TryComplete(false, 90, Now).Should().BeFalse("a quiz is still failed");
        e.TryComplete(true, 90, Now).Should().BeTrue();
        e.Status.Should().Be(EnrolmentStatus.Completed);
        e.FinalScore.Should().Be(90m);
        e.TryComplete(true, 90, Now).Should().BeFalse("completes once");
        e.Withdraw().Error.Code.Should().Be("enrolment_not_active");
    }

    [Fact]
    public void Forum_moderation_flags_contact_details_and_off_platform_payments()
    {
        Moderation.Scan("Great lesson, thanks!").Should().BeNull();
        Moderation.Scan("Call me on 08031234567 for private classes").Should().Be("contact_details");
        Moderation.Scan("You can pay me directly and skip the fees").Should().Be("off_platform_payment");
        var post = ForumPost.Create(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), "WhatsApp me for the slides", Now);
        post.IsFlagged.Should().BeTrue();
        post.FlagReason.Should().Be("off_platform_payment");
    }

    [Fact]
    public void Cohort_capacity_is_enforced()
    {
        var cohort = Cohort.Create(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Jan intake", Now, Now.AddDays(60), 2, null).Value;
        cohort.Admit().IsSuccess.Should().BeTrue();
        cohort.Admit().IsSuccess.Should().BeTrue();
        cohort.Admit().Error.Code.Should().Be("cohort_full");
        cohort.Release();
        cohort.HasSeats.Should().BeTrue();
        Cohort.Create(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "x", Now, Now, 1, null).Error.Code.Should().Be("cohort_dates_invalid");
    }
}
