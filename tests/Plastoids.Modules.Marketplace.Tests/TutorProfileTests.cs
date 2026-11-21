using Plastoids.Modules.Tutors.Domain;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Marketplace.Tests;

public class TutorProfileTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private static TutorProfile Applied() => TutorProfile.Apply(Tenants.Marketplace, Guid.CreateVersion7(), "Senior data analyst", "I teach SQL and Python to beginners with real datasets.",
        ["SQL", " python ", "sql"], ["en"], ["one_to_one", "course"], "Africa/Lagos", Money.NairaAmount(8000), true, Now).Value;

    [Fact]
    public void Apply_normalises_tags_and_starts_as_new_applied()
    {
        var p = Applied();
        p.Subjects.Should().Equal("sql", "python");
        p.Level.Should().Be(TutorLevel.New);
        p.Status.Should().Be(TutorStatus.Applied);
        p.CodeOfConductAcceptedAt.Should().Be(Now);
        p.IsVerified.Should().BeFalse();
    }

    [Fact]
    public void Apply_requires_code_of_conduct_valid_time_zone_and_naira_rate()
    {
        TutorProfile.Apply(Tenants.Marketplace, Guid.NewGuid(), "h", "b", ["x"], ["en"], ["course"], "Africa/Lagos", Money.NairaAmount(1), false, Now).Error.Code.Should().Be("code_of_conduct_required");
        TutorProfile.Apply(Tenants.Marketplace, Guid.NewGuid(), "h", "b", ["x"], ["en"], ["course"], "Mars/Olympus", Money.NairaAmount(1), true, Now).Error.Code.Should().Be("time_zone_invalid");
        TutorProfile.Apply(Tenants.Marketplace, Guid.NewGuid(), "h", "b", ["x"], ["en"], ["course"], "Africa/Lagos", new Money(100, "USD"), true, Now).Error.Code.Should().Be("hourly_rate_invalid");
    }

    [Fact]
    public void Verification_needs_all_three_stages_and_fires_once()
    {
        var p = Applied();
        p.TryVerify(Now).Should().BeFalse();
        p.RecordIdentityResult(true, "ADA OKAFOR", Now);
        p.TryVerify(Now).Should().BeFalse("qualifications and subject still pending");
        p.RecordQualificationsReviewed(Now);
        p.TryVerify(Now).Should().BeFalse();
        p.SubmitDemoLesson("demo.mp4", Now).IsSuccess.Should().BeTrue();
        p.RecordSubjectReviewed(Now);
        p.TryVerify(Now).Should().BeTrue();
        p.IsVerified.Should().BeTrue();
        p.VerifiedAt.Should().Be(Now);
        p.TryVerify(Now).Should().BeFalse("already verified");
        p.SubmitDemoLesson("again.mp4", Now).Error.Code.Should().Be("subject_already_reviewed");
    }

    [Fact]
    public void Failed_identity_does_not_count()
    {
        var p = Applied();
        p.RecordIdentityResult(false, null, Now);
        p.IdentityPassed.Should().BeFalse();
    }

    [Fact]
    public void Level_rises_after_ten_sessions_with_rating_at_least_4_5()
    {
        var p = Applied();
        for (var i = 0; i < 9; i++)
        {
            p.RecordOutcome(1, 0, 5);
        }

        p.Level.Should().Be(TutorLevel.New);
        p.RecordOutcome(1, 0, 4);
        p.Level.Should().Be(TutorLevel.Rising);
        p.RatingCount.Should().Be(10);
        p.QualityScore.Should().BeGreaterThan(0.9m);
    }

    [Fact]
    public void Level_does_not_rise_with_low_rating()
    {
        var p = Applied();
        for (var i = 0; i < 25; i++)
        {
            p.RecordOutcome(0, 1, 3);
        }

        p.Level.Should().Be(TutorLevel.New);
    }
}
