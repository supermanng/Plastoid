using Plastoids.Infrastructure.Time;
using Plastoids.Modules.Tutors.Domain;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Marketplace.Tests;

public class TutorSupportingDomainTests
{
    private static readonly DateTime Now = new(2026, 11, 21, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("Ada Okafor", "OKAFOR ADA", true)]
    [InlineData("Ada Chioma Okafor", "ADA OKAFOR", true)]
    [InlineData("Ada Okafor", "ADA C. OKAFOR", true)]
    [InlineData("Ada Okafor", "TUNDE BAKARE", false)]
    [InlineData("Ada Okafor", "ADA BAKARE", false)]
    [InlineData("", "ADA", false)]
    public void Payout_name_match_ignores_order_case_and_middle_names(string verified, string bank, bool expected)
    {
        PayoutAccount.NamesMatch(verified, bank).Should().Be(expected);
    }

    [Fact]
    public void Working_days_skip_weekends()
    {
        var friday = new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc);
        WorkingDays.Add(friday, 3).Should().Be(new DateTime(2026, 11, 25, 9, 0, 0, DateTimeKind.Utc));
        WorkingDays.Add(friday, 5).Should().Be(new DateTime(2026, 11, 27, 9, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Review_queue_items_are_overdue_only_once_and_not_after_completion()
    {
        var item = ReviewQueueItem.Enqueue(Tenants.Marketplace, Guid.NewGuid(), ReviewKinds.Credential, Guid.NewGuid(), Now, Now.AddDays(3));
        item.IsOverdue(Now.AddDays(2)).Should().BeFalse();
        item.IsOverdue(Now.AddDays(4)).Should().BeTrue();
        item.MarkAlerted(Now.AddDays(4));
        item.IsOverdue(Now.AddDays(5)).Should().BeFalse("alert raised once");
        var done = ReviewQueueItem.Enqueue(Tenants.Marketplace, Guid.NewGuid(), ReviewKinds.Subject, Guid.NewGuid(), Now, Now.AddDays(5));
        done.Complete(Guid.NewGuid(), Now.AddDays(1));
        done.IsOverdue(Now.AddDays(9)).Should().BeFalse();
    }

    [Fact]
    public void Credential_can_be_reviewed_once()
    {
        var c = Credential.Submit(Tenants.Marketplace, Guid.NewGuid(), CredentialTypes.Degree, "UNILAG", "BSc Computer Science", "credentials/x.pdf");
        c.Review(true, Guid.NewGuid(), null, Now).IsSuccess.Should().BeTrue();
        c.Status.Should().Be(CredentialStatus.Approved);
        c.Review(false, Guid.NewGuid(), "late", Now).Error.Code.Should().Be("credential_already_reviewed");
    }

    [Fact]
    public void Availability_projects_weekly_rules_and_exceptions_to_utc_slots()
    {
        var tutor = Guid.NewGuid();
        var rules = new List<Availability>
        {
            Availability.Weekly(Tenants.Marketplace, tutor, DayOfWeek.Monday, new TimeOnly(9, 0), new TimeOnly(12, 0)),
            // Monday 23 Nov 2026: 10:00–11:00 blocked.
            Availability.Exception(Tenants.Marketplace, tutor, new DateOnly(2026, 11, 23), new TimeOnly(10, 0), new TimeOnly(11, 0), available: false),
            // Tuesday 24 Nov: extra window.
            Availability.Exception(Tenants.Marketplace, tutor, new DateOnly(2026, 11, 24), new TimeOnly(18, 0), new TimeOnly(19, 0), available: true),
        };

        var slots = AvailabilityCalendar.Slots(rules, "Africa/Lagos", new DateOnly(2026, 11, 23), 2);

        // Lagos is UTC+1 all year: 09:00 local → 08:00Z.
        slots.Select(s => s.StartUtc).Should().Equal(
            new DateTime(2026, 11, 23, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 11, 24, 17, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Availability_honours_buffer_between_slots()
    {
        var rules = new[] { Availability.Weekly(Tenants.Marketplace, Guid.NewGuid(), DayOfWeek.Wednesday, new TimeOnly(9, 0), new TimeOnly(12, 0)) };
        var slots = AvailabilityCalendar.Slots(rules, "UTC", new DateOnly(2026, 11, 25), 1, slotMinutes: 60, bufferMinutes: 30);
        slots.Should().HaveCount(2);
        slots[1].StartUtc.Should().Be(new DateTime(2026, 11, 25, 10, 30, 0, DateTimeKind.Utc));
    }
}
