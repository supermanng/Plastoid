using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Sponsorship.Services;

/// <summary>ADR 0021 <c>SeatBlockView</c>: one purchase of seats in a cohort as the sponsor and staff see it. <c>Open = seats − allocated − released</c>.</summary>
public record SeatBlockView(
    Guid BlockId,
    Guid OrganisationId,
    string SponsorName,
    Guid CohortId,
    string CohortName,
    Guid CourseId,
    string CourseTitle,
    string CourseSlug,
    DateTime StartsAt,
    DateTime EndsAt,
    int Seats,
    int Allocated,
    int Enrolled,
    int Invited,
    int Released,
    int Open,
    Money PricePerSeat,
    long UnitsRedeemed,
    long UnitRateKobo,
    string Status,
    DateTime PurchasedAt,
    DateTime? ClosedAt);

/// <summary>ADR 0021 <c>SeatAllocationView</c>: Contact is the masked phone ("+234803****01") or the email; the phone number itself is never stored.</summary>
/// <summary>ContactKind "phone" | "email"; InviteSentAt / InviteSendCount say whether the invite message went out (invited seats only).</summary>
public sealed record SeatAllocationView(Guid AllocationId, Guid BlockId, int SeatNo, string Status, Guid? LearnerId, string DisplayName, string Contact, DateTime InvitedAt, DateTime? EnrolledAt, Guid? EnrolmentId,
    string ContactKind = "phone", DateTime? InviteSentAt = null, int InviteSendCount = 0);

/// <summary>ADR 0021 <c>SeatBlockDetail</c>: the view's fields plus the allocations and the organisation's current unit balance.</summary>
public sealed record SeatBlockDetail(
    Guid BlockId,
    Guid OrganisationId,
    string SponsorName,
    Guid CohortId,
    string CohortName,
    Guid CourseId,
    string CourseTitle,
    string CourseSlug,
    DateTime StartsAt,
    DateTime EndsAt,
    int Seats,
    int Allocated,
    int Enrolled,
    int Invited,
    int Released,
    int Open,
    Money PricePerSeat,
    long UnitsRedeemed,
    long UnitRateKobo,
    string Status,
    DateTime PurchasedAt,
    DateTime? ClosedAt,
    IReadOnlyList<SeatAllocationView> Allocations,
    long WalletBalance)
    : SeatBlockView(BlockId, OrganisationId, SponsorName, CohortId, CohortName, CourseId, CourseTitle, CourseSlug, StartsAt, EndsAt, Seats, Allocated, Enrolled, Invited, Released, Open, PricePerSeat, UnitsRedeemed, UnitRateKobo, Status, PurchasedAt, ClosedAt);

internal static class SeatViews
{
    public static SeatBlockView Block(SeatBlock block, IEnumerable<SeatAllocation> allocations)
    {
        var enrolled = 0;
        var invited = 0;
        foreach (var allocation in allocations)
        {
            if (allocation.Status == SeatAllocationStatus.Enrolled)
            {
                enrolled++;
            }
            else
            {
                invited++;
            }
        }

        return Block(block, enrolled, invited);
    }

    public static SeatBlockView Block(SeatBlock block, int enrolled, int invited) => new(
        block.Id, block.OrganisationId, block.SponsorName, block.CohortId, block.CohortName, block.CourseId, block.CourseTitle, block.CourseSlug, block.StartsAt, block.EndsAt,
        block.Seats, block.Allocated, enrolled, invited, block.Released, block.Open, block.PricePerSeat, block.UnitsRedeemed, block.UnitRateKobo,
        block.Status.ToString().ToLowerInvariant(), block.PurchasedAt, block.ClosedAt);

    public static SeatAllocationView Allocation(SeatAllocation a)
        => new(a.Id, a.BlockId, a.SeatNo, a.Status.ToString().ToLowerInvariant(), a.LearnerId, a.DisplayName, a.ContactMasked, a.InvitedAt, a.EnrolledAt, a.EnrolmentId, a.ContactKind, a.InviteSentAt, a.InviteSendCount);

    public static SeatBlockDetail Detail(SeatBlock block, IReadOnlyList<SeatAllocation> allocations, long walletBalance)
    {
        var v = Block(block, allocations);
        return new SeatBlockDetail(v.BlockId, v.OrganisationId, v.SponsorName, v.CohortId, v.CohortName, v.CourseId, v.CourseTitle, v.CourseSlug, v.StartsAt, v.EndsAt, v.Seats, v.Allocated, v.Enrolled, v.Invited, v.Released, v.Open,
            v.PricePerSeat, v.UnitsRedeemed, v.UnitRateKobo, v.Status, v.PurchasedAt, v.ClosedAt, allocations.OrderBy(a => a.SeatNo).Select(Allocation).ToList(), walletBalance);
    }
}
