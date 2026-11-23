using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payouts.Domain;
using Plastoids.Modules.Sponsorship.Contracts;

namespace Plastoids.Modules.Payouts.Services;

/// <summary>
/// The earnings one allocated sponsored seat produces (ADR 0021), shaped like a course sale: the payee's net under
/// <c>seat:&lt;allocationId&gt;</c> and, on an organisation course with an engaged tutor, the tutor's share under
/// <c>seat:&lt;allocationId&gt;:tutor</c>. Both hold for the course-sale window.
/// </summary>
internal static class SeatEarnings
{
    public static string KeyFor(Guid allocationId) => "seat:" + allocationId.ToString("N");

    public static IReadOnlyList<Earning> Build(Guid tenantId, SeatAllocated @event, DateTime now, int holdDays)
    {
        var key = KeyFor(@event.AllocationId);
        var releasesAt = now.AddDays(holdDays);
        var earnings = new List<Earning>
        {
            Earning.Record(tenantId, @event.PayeeType, @event.PayeeId, EarningSources.Seat, @event.AllocationId, @event.BlockId, key, @event.Price, @event.PayeeNet, now, releasesAt),
        };
        if (@event.PayeeType == PayeeTypes.Organisation && @event.TutorUserId is { } tutor && !@event.TutorShare.IsZero)
        {
            earnings.Add(Earning.Record(tenantId, PayeeTypes.Tutor, tutor, EarningSources.Seat, @event.AllocationId, @event.BlockId, key + ":tutor", @event.Price, @event.TutorShare, now, releasesAt));
        }

        return earnings;
    }
}
