using Plastoids.Modules.Catalogue.Domain;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Marketplace.Tests;

public class CatalogueDomainTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    private static Course Draft() => Course.Create(Tenants.Marketplace, CourseOwnerType.Tutor, Guid.NewGuid(), Guid.NewGuid(), "Data Analysis with SQL: From Zero!", "A practical course.", CourseLevels.Beginner, "EN", "Data", ["SQL", "sql", " Excel "], "abc123");

    [Fact]
    public void Create_slugifies_and_normalises()
    {
        var c = Draft();
        c.Slug.Should().Be("data-analysis-with-sql-from-zero-abc123");
        c.Language.Should().Be("en");
        c.Category.Should().Be("data");
        c.Skills.Should().Equal("sql", "excel");
        c.Status.Should().Be(CourseStatus.Draft);
    }

    [Fact]
    public void Publish_requires_verified_owner_and_approval_then_increments_version()
    {
        var c = Draft();
        c.Publish(true, Now).Error.Code.Should().Be("course_not_approved");
        c.Submit(Now).IsSuccess.Should().BeTrue();
        c.UpdateOutline("New title", null, null, null, null, null).Error.Code.Should().Be("course_in_review");
        c.RecordReview(true, null).IsSuccess.Should().BeTrue();
        c.Publish(false, Now).Error.Code.Should().Be("tutor_not_verified");
        c.Publish(true, Now).Value.Should().Be(1);
        c.Status.Should().Be(CourseStatus.Published);
        c.PublishedAt.Should().Be(Now);
    }

    [Fact]
    public void Editing_an_approved_course_sends_it_back_to_draft()
    {
        var c = Draft();
        c.Submit(Now);
        c.RecordReview(true, null);
        c.Status.Should().Be(CourseStatus.Approved);
        c.UpdateOutline(null, "Changed summary", null, null, null, null).IsSuccess.Should().BeTrue();
        c.Status.Should().Be(CourseStatus.Draft);
    }

    [Fact]
    public void Human_review_for_first_three_courses_then_sampled()
    {
        Course.RequiresHumanReview(0, Guid.NewGuid()).Should().BeTrue();
        Course.RequiresHumanReview(2, Guid.NewGuid()).Should().BeTrue();
        var sampled = Enumerable.Range(0, 500).Count(_ => Course.RequiresHumanReview(3, Guid.CreateVersion7()));
        sampled.Should().BeInRange(50, 160, "roughly one in five is sampled");
    }

    [Fact]
    public void Rating_average_updates_incrementally()
    {
        var c = Draft();
        c.RecordRating(5);
        c.RecordRating(4);
        c.RatingAverage.Should().Be(4.5m);
        c.RatingCount.Should().Be(2);
    }

    [Fact]
    public void Offering_rules_for_cohort_and_funding_types()
    {
        var courseId = Guid.NewGuid();
        Offering.Create(Tenants.Marketplace, courseId, OfferingFormats.SelfPaced, Money.NairaAmount(45_000), ["self", "sponsored"], null, null, null, Now).IsSuccess.Should().BeTrue();
        Offering.Create(Tenants.Marketplace, courseId, OfferingFormats.Cohort, Money.NairaAmount(45_000), ["self"], 30, null, 8, Now).Error.Code.Should().Be("cohort_start_required");
        Offering.Create(Tenants.Marketplace, courseId, OfferingFormats.Cohort, Money.NairaAmount(45_000), ["self"], null, Now.AddDays(10), 8, Now).Error.Code.Should().Be("capacity_required");
        Offering.Create(Tenants.Marketplace, courseId, OfferingFormats.SelfPaced, Money.NairaAmount(1), ["crypto"], null, null, null, Now).Error.Code.Should().Be("funding_types_invalid");
        Offering.Create(Tenants.Marketplace, courseId, OfferingFormats.SelfPaced, new Money(100, "USD"), ["self"], null, null, null, Now).Error.Code.Should().Be("price_invalid");
        var sponsored = Offering.Create(Tenants.Marketplace, courseId, OfferingFormats.Cohort, Money.Zero(), ["sponsored"], 100, Now.AddDays(30), 12, Now).Value;
        sponsored.AcceptsSponsorship.Should().BeTrue();
    }

    [Fact]
    public void Money_serialises_as_amount_and_currency()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(Money.NairaAmount(45_000));
        json.Should().Be("{\"amount\":4500000,\"currency\":\"NGN\"}");
        System.Text.Json.JsonSerializer.Deserialize<Money>("{\"amount\":1500,\"currency\":\"NGN\"}").Should().Be(Money.Kobo(1500));
        System.Text.Json.JsonSerializer.Deserialize<Money>("{\"amount\":1500}").Should().Be(Money.Kobo(1500));
    }

    [Fact]
    public void Media_asset_lifecycle()
    {
        var a = MediaAsset.Create(Tenants.Marketplace, Guid.NewGuid(), "fake", "upl_1");
        a.Status.Should().Be(MediaStatus.PendingUpload);
        a.MarkProcessing();
        a.MarkReady("pb_1", 600, "240p,480p,720p");
        a.Status.Should().Be(MediaStatus.Ready);
        a.PlaybackId.Should().Be("pb_1");
    }
}
