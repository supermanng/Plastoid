using System.Globalization;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Lms.Contracts;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Wallet.Contracts;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Sponsorship.Services;

/// <summary>
/// Turns an allocation into an enrolled seat (ADR 0021 §3), shared by AllocateSeat, the user-registered / email-linked consumers
/// and the close job: split the price, post entry 15, mark the allocation enrolled, then tell Lms, Payouts and the learner.
/// The seat itself was already counted on the block when it was taken.
/// </summary>
internal sealed class SeatAllocator(SeatPricing pricing, ILedgerService ledger, IOutbox outbox)
{
    public async Task<Result> EnrolAsync(SeatBlock block, SeatAllocation allocation, Guid learnerId, DateTime now, CancellationToken cancellationToken)
    {
        if (allocation.Status == SeatAllocationStatus.Enrolled)
        {
            return allocation.LearnerId == learnerId ? Result.Success() : Error.Conflict("seat_already_enrolled", "The seat is already enrolled by another learner.");
        }

        var split = await pricing.SplitAsync(block, cancellationToken);
        if (split.IsFailure)
        {
            return Result.Failure(split.Error);
        }

        var enrolled = allocation.Enrol(learnerId, split.Value, null, now);
        if (enrolled.IsFailure)
        {
            return enrolled;
        }

        var posted = await ledger.PostAsync(SponsorshipPostings.Entry15SeatAllocated(block, allocation, now), cancellationToken);
        if (posted.IsFailure)
        {
            return Result.Failure(posted.Error);
        }

        allocation.LinkLedgerEntry(posted.Value);
        outbox.Enqueue(new SeatAllocated(block.Id, block.Id, allocation.Id, block.OrganisationId, block.SponsorName, block.CohortId, block.CohortName, block.CourseId, block.CourseTitle, block.OfferingId, learnerId,
            block.PricePerSeat, split.Value.PayeeType, split.Value.PayeeId, split.Value.PayeeNet, block.TutorUserId, split.Value.TutorShare, split.Value.Commission));
        outbox.Enqueue(NotificationRequested.To(learnerId, "sponsorship.seat.allocated", new Dictionary<string, string>
        {
            ["sponsorName"] = block.SponsorName,
            ["cohortName"] = block.CohortName,
            ["courseTitle"] = block.CourseTitle,
            ["startsAt"] = block.StartsAt.ToString("O", CultureInfo.InvariantCulture),
        }));
        return Result.Success();
    }
}

/// <summary>
/// Gives open seats back (ADR 0021 §4), shared by ReleaseSeats and the close job: frees the cohort reservation, credits the
/// units to the wallet pro rata to what was redeemed (kind release) and posts entry 16, then counts the seats released on the block.
/// </summary>
internal sealed class SeatReleaser(ICohortSeats cohorts, IUnitWallet wallets, ILedgerService ledger)
{
    public const string CloseoutSuffix = "closeout";

    /// <summary><paramref name="referenceSuffix"/> is the running released count by default, or "closeout" when the cohort ended.</summary>
    public async Task<Result> ReleaseAsync(SeatBlock block, int seats, DateTime now, CancellationToken cancellationToken, string? referenceSuffix = null)
    {
        if (block.Status != SeatBlockStatus.Active)
        {
            return Error.Conflict("block_closed", "The seat block is closed.");
        }

        if (seats < 1)
        {
            return Error.Validation("seats_invalid", "Release at least one seat.");
        }

        if (seats > block.Open)
        {
            return Error.Conflict("release_exceeds_open", $"Only {block.Open} seats are open in this block.");
        }

        var freed = await cohorts.ReleaseAsync(block.CohortId, seats, cancellationToken);
        if (freed.IsFailure)
        {
            return freed;
        }

        var suffix = referenceSuffix ?? (block.Released + seats).ToString(CultureInfo.InvariantCulture);
        var reference = SponsorshipPostings.ReleaseReference(block.Id, suffix);
        var value = block.PriceOf(seats);
        var credited = await wallets.CreditAsync(new UnitCreditRequest(WalletOwnerTypes.Organisation, block.OrganisationId, block.UnitsFor(seats), value, reference, WalletTransactionKinds.Release, "seat_block", block.Id, $"{seats} seats released from {block.CohortName}"), cancellationToken);
        if (credited.IsFailure)
        {
            return Result.Failure(credited.Error);
        }

        var posted = await ledger.PostAsync(SponsorshipPostings.Entry16SeatsReleased(block, seats, value, credited.Value.WalletId, now, suffix), cancellationToken);
        if (posted.IsFailure)
        {
            return Result.Failure(posted.Error);
        }

        return block.Release(seats);
    }
}
