using Plastoids.Modules.Catalogue.Contracts;
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

    [Fact]
    public void Organisation_course_is_owned_by_the_organisation_and_publishes_with_its_own_verification_code()
    {
        var organisationId = Guid.NewGuid();
        var author = Guid.NewGuid();
        var c = Course.CreateForOrganisation(Tenants.Marketplace, organisationId, author, "Cloud Fundamentals for Teams", "Summary", CourseLevels.Beginner, "en", "cloud", ["aws"], "org001");
        c.OwnerType.Should().Be(CourseOwnerType.Organisation);
        c.IsOrganisationOwned.Should().BeTrue();
        c.OwnerTypeName.Should().Be(CourseOwnerTypes.Organisation);
        c.OwnerId.Should().Be(organisationId);
        c.OrganisationId.Should().Be(organisationId);
        c.OwnerUserId.Should().Be(author);
        c.Submit(Now);
        c.RecordReview(true, null);
        c.Publish(false, Now).Error.Code.Should().Be("organisation_not_verified");
        c.Publish(true, Now).Value.Should().Be(1);
        Draft().OwnerNotVerified().Code.Should().Be("tutor_not_verified");
    }

    [Fact]
    public void Offering_assigns_an_engaged_tutor()
    {
        var o = Offering.Create(Tenants.Marketplace, Guid.NewGuid(), OfferingFormats.Cohort, Money.NairaAmount(45_000), ["self"], 30, Now.AddDays(10), 8, Now).Value;
        o.TutorUserId.Should().BeNull();
        var tutor = Guid.NewGuid();
        var engagement = Guid.NewGuid();
        o.AssignTutor(tutor, engagement);
        o.TutorUserId.Should().Be(tutor);
        o.EngagementId.Should().Be(engagement);
    }

    [Fact]
    public void Engagement_validates_terms_on_offer()
    {
        Engagement.Offer(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new EngagementTerms("hourly", 0, null), null, Guid.NewGuid(), Now).Error.Code.Should().Be("engagement_terms_invalid");
        Engagement.Offer(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new EngagementTerms(EngagementTermKinds.RevenueShare, 0, null), null, Guid.NewGuid(), Now).Error.Code.Should().Be("engagement_share_invalid");
        Engagement.Offer(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new EngagementTerms(EngagementTermKinds.RevenueShare, 10_001, null), null, Guid.NewGuid(), Now).Error.Code.Should().Be("engagement_share_invalid");
        Engagement.Offer(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new EngagementTerms(EngagementTermKinds.PerEnrolment, 0, null), null, Guid.NewGuid(), Now).Error.Code.Should().Be("engagement_fee_invalid");
        Engagement.Offer(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new EngagementTerms(EngagementTermKinds.PerEnrolment, 0, Money.Zero()), null, Guid.NewGuid(), Now).Error.Code.Should().Be("engagement_fee_invalid");
        Engagement.Offer(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new EngagementTerms(EngagementTermKinds.PerEnrolment, 0, new Money(500, "USD")), null, Guid.NewGuid(), Now).Error.Code.Should().Be("engagement_fee_invalid");

        var perEnrolment = Engagement.Offer(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new EngagementTerms(EngagementTermKinds.PerEnrolment, 9_999, Money.NairaAmount(5_000)), "  Welcome aboard ", Guid.NewGuid(), Now).Value;
        perEnrolment.ShareBps.Should().Be(0, "per_enrolment ignores shareBps");
        perEnrolment.FixedFee.Should().Be(Money.NairaAmount(5_000));
        perEnrolment.Note.Should().Be("Welcome aboard");
        perEnrolment.ToTerms().Should().Be(new EngagementTerms(EngagementTermKinds.PerEnrolment, 0, Money.NairaAmount(5_000)));

        var revenueShare = Engagement.Offer(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new EngagementTerms(EngagementTermKinds.RevenueShare, 4_000, Money.NairaAmount(1)), null, Guid.NewGuid(), Now).Value;
        revenueShare.FixedFee.Should().BeNull("revenue_share ignores fixedFee");
        revenueShare.ToTerms().Should().Be(new EngagementTerms(EngagementTermKinds.RevenueShare, 4_000, null));
    }

    [Fact]
    public void Engagement_moves_offered_to_active_to_ended_and_rejects_other_transitions()
    {
        var e = Engagement.Offer(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new EngagementTerms(EngagementTermKinds.RevenueShare, 4_000, null), "Teach the Jan cohort", Guid.NewGuid(), Now).Value;
        e.Status.Should().Be(EngagementStatus.Offered);
        e.IsOpen.Should().BeTrue();
        e.OfferedAt.Should().Be(Now);
        e.End("too early", Now).Error.Code.Should().Be("engagement_not_active");

        e.Accept(null, Now.AddHours(1)).IsSuccess.Should().BeTrue();
        e.Status.Should().Be(EngagementStatus.Active);
        e.RespondedAt.Should().Be(Now.AddHours(1));
        e.Note.Should().Be("Teach the Jan cohort", "an empty response note keeps the offer note");
        e.Accept(null, Now).Error.Code.Should().Be("engagement_not_offered");
        e.Decline(null, Now).Error.Code.Should().Be("engagement_not_offered");

        e.End("Cohort finished", Now.AddDays(90)).IsSuccess.Should().BeTrue();
        e.Status.Should().Be(EngagementStatus.Ended);
        e.IsOpen.Should().BeFalse();
        e.EndedAt.Should().Be(Now.AddDays(90));
        e.EndReason.Should().Be("Cohort finished");
        e.End(null, Now).Error.Code.Should().Be("engagement_not_active");
    }

    [Fact]
    public void Engagement_can_be_declined_with_a_note()
    {
        var e = Engagement.Offer(Tenants.Marketplace, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new EngagementTerms(EngagementTermKinds.RevenueShare, 2_500, null), null, Guid.NewGuid(), Now).Value;
        e.Decline("Fully booked this quarter", Now).IsSuccess.Should().BeTrue();
        e.Status.Should().Be(EngagementStatus.Declined);
        e.Note.Should().Be("Fully booked this quarter");
        e.IsOpen.Should().BeFalse();
        e.Accept(null, Now).Error.Code.Should().Be("engagement_not_offered");
    }

    [Fact]
    public void Revenue_share_rounds_half_kobo_to_even_and_never_exceeds_the_net()
    {
        var share = new EngagementTerms(EngagementTermKinds.RevenueShare, 4_000, null);
        share.TutorShareOf(Money.NairaAmount(38_250)).Should().Be(Money.NairaAmount(15_300));
        // 3 825 001 kobo × 40 % = 1 530 000.4 → 1 530 000; 3 825 005 × 40 % = 1 530 002.0 (exact).
        share.TutorShareOf(Money.Kobo(3_825_001)).Should().Be(Money.Kobo(1_530_000));
        // 25 bps of 1 kobo = 0.0025 → 0; 3 333 bps of 15 kobo = 4.9995 → 5.
        new EngagementTerms(EngagementTermKinds.RevenueShare, 25, null).TutorShareOf(Money.Kobo(1)).Should().Be(Money.Zero());
        new EngagementTerms(EngagementTermKinds.RevenueShare, 3_333, null).TutorShareOf(Money.Kobo(15)).Should().Be(Money.Kobo(5));
        // Exactly half a kobo rounds to even: 5 000 bps of 1 kobo = 0.5 → 0; of 3 kobo = 1.5 → 2.
        new EngagementTerms(EngagementTermKinds.RevenueShare, 5_000, null).TutorShareOf(Money.Kobo(1)).Should().Be(Money.Zero());
        new EngagementTerms(EngagementTermKinds.RevenueShare, 5_000, null).TutorShareOf(Money.Kobo(3)).Should().Be(Money.Kobo(2));
        new EngagementTerms(EngagementTermKinds.RevenueShare, 10_000, null).TutorShareOf(Money.NairaAmount(100)).Should().Be(Money.NairaAmount(100));
    }

    [Fact]
    public void Per_enrolment_fee_is_capped_at_the_net_and_zero_net_pays_nothing()
    {
        var fee = new EngagementTerms(EngagementTermKinds.PerEnrolment, 0, Money.NairaAmount(5_000));
        fee.TutorShareOf(Money.NairaAmount(38_250)).Should().Be(Money.NairaAmount(5_000));
        fee.TutorShareOf(Money.NairaAmount(3_000)).Should().Be(Money.NairaAmount(3_000), "the fee is capped at the sale net of commission");
        fee.TutorShareOf(Money.Zero()).Should().Be(Money.Zero());
        fee.TutorShareOf(Money.Kobo(-100)).Should().Be(Money.Zero());
        new EngagementTerms(EngagementTermKinds.PerEnrolment, 0, null).TutorShareOf(Money.NairaAmount(10)).Should().Be(Money.Zero(), "no fee means no share");
        new EngagementTerms(EngagementTermKinds.RevenueShare, 4_000, null).TutorShareOf(Money.Zero()).Should().Be(Money.Zero());
    }
}
