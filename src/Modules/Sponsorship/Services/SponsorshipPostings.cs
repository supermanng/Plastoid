using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Sponsorship.Services;

/// <summary>Journal entries 7–10 of blueprint §18, drawn from programme funds held (2020, sub-ledger per programme).</summary>
internal static partial class SponsorshipPostings
{
    /// <summary>Entry 7: a seat is filled → the platform seat fee is earned. Dr 2020 (programme) / Cr 4040.</summary>
    public static JournalEntryDraft Entry7SeatFee(Seat seat, DateTime occurredAt)
        => new($"seat:{seat.Id:N}:fill:{seat.FillCount}", "seat_fee_earned", $"Seat fee earned, seat {seat.SeatNo}",
            [PostingDraft.Dr(AccountCodes.ProgrammeFundsHeld, FeeSchedule.SeatFee, SubLedgerOwnerTypes.Programme, seat.ProgrammeId), PostingDraft.Cr(AccountCodes.PlatformSeatFee, FeeSchedule.SeatFee)], occurredAt);

    /// <summary>Entry 8: each programme month the management fee on filled seats is earned. Dr 2020 / Cr 4030.</summary>
    public static JournalEntryDraft Entry8ManagementFee(Programme programme, int monthIndex, Money amount, DateTime occurredAt)
        => new($"programme:{programme.Id:N}:month:{monthIndex}:management", "programme_management_fee", $"Management fee month {monthIndex} of {programme.DurationMonths}",
            [PostingDraft.Dr(AccountCodes.ProgrammeFundsHeld, amount, SubLedgerOwnerTypes.Programme, programme.Id), PostingDraft.Cr(AccountCodes.ProgrammeManagementFee, amount)], occurredAt);

    /// <summary>Entry 9: each programme month tutors earn for sessions delivered. Dr 2020 / Cr 2010 (tutor).</summary>
    public static JournalEntryDraft Entry9TutorEarnings(Programme programme, int monthIndex, Money amount, DateTime occurredAt)
        => new($"programme:{programme.Id:N}:month:{monthIndex}:tuition", "programme_tutor_earnings", $"Tutor earnings month {monthIndex} of {programme.DurationMonths}",
            [PostingDraft.Dr(AccountCodes.ProgrammeFundsHeld, amount, SubLedgerOwnerTypes.Programme, programme.Id), PostingDraft.Cr(AccountCodes.TutorPayable, amount, SubLedgerOwnerTypes.Tutor, programme.TutorUserId)], occurredAt);

    /// <summary>Entry 10: close-out refund of unfilled seats to the sponsor. Dr 2020 / Cr 1020.</summary>
    public static JournalEntryDraft Entry10Closeout(Programme programme, Money refund, int unfilledSeats, DateTime occurredAt)
        => new($"programme:{programme.Id:N}:closeout", "programme_closeout_refund", $"Close-out: {unfilledSeats} unfilled seats refunded",
            [PostingDraft.Dr(AccountCodes.ProgrammeFundsHeld, refund, SubLedgerOwnerTypes.Programme, programme.Id), PostingDraft.Cr(AccountCodes.BankClientFunds, refund)], occurredAt);

    /// <summary>Monthly amounts for <paramref name="filledSeats"/>: management fee per seat ÷ months and sponsor tuition share per seat ÷ months (entries 8 and 9).</summary>
    public static (Money ManagementFee, Money TutorEarnings) MonthlyAmounts(Programme programme, int filledSeats)
    {
        var months = programme.DurationMonths;
        var quote = programme.Quote;
        var management = new Money(checked(quote.ManagementFeePerSeat.AmountKobo * filledSeats) / months, quote.ManagementFee.Currency);
        var tuition = new Money(checked(quote.TuitionPerSeatSponsorShare.AmountKobo * filledSeats) / months, quote.Tuition.Currency);
        return (management, tuition);
    }
}

/// <summary>Journal entries 14–16 of ADR 0021: a seat block is paid with units, a seat is allocated, open seats are released.</summary>
internal static partial class SponsorshipPostings
{
    /// <summary>Entry 14: units released from the wallet fund the block. Dr 2050 (wallet) the naira released; Cr 2020 (block) Seats × price; any gap to 4060.</summary>
    public static JournalEntryDraft Entry14SeatBlockPurchase(SeatBlock block, Money unitsValue, Guid walletId, DateTime occurredAt)
    {
        // Units from a goodwill adjustment carry no naira (ADR 0018): nothing leaves 2050 and the gap below expenses the whole price to 4060.
        var postings = new List<PostingDraft>();
        if (!unitsValue.IsZero)
        {
            postings.Add(PostingDraft.Dr(AccountCodes.UnitBalancesHeld, unitsValue, SubLedgerOwnerTypes.Wallet, walletId));
        }

        postings.Add(PostingDraft.Cr(AccountCodes.ProgrammeFundsHeld, block.TotalPrice, SubLedgerOwnerTypes.Programme, block.Id));
        AddRevaluation(postings, unitsValue, block.TotalPrice);
        return new JournalEntryDraft(PurchaseReference(block.Id), "seat_block_purchase", $"{block.Seats} seats in {block.CohortName} paid with {block.UnitsRedeemed} units", postings, occurredAt);
    }

    /// <summary>Entry 15: a seat is given to a learner. Dr 2020 (block) the price; Cr 4010 commission; Cr 2010 the engaged tutor's share; Cr 2060 (organisation) or 2010 (tutor) the payee's net.</summary>
    public static JournalEntryDraft Entry15SeatAllocated(SeatBlock block, SeatAllocation allocation, DateTime occurredAt)
    {
        var postings = new List<PostingDraft> { PostingDraft.Dr(AccountCodes.ProgrammeFundsHeld, block.PricePerSeat, SubLedgerOwnerTypes.Programme, block.Id) };
        if (!allocation.Commission.IsZero)
        {
            postings.Add(PostingDraft.Cr(AccountCodes.CourseCommission, allocation.Commission));
        }

        if (!allocation.TutorShare.IsZero && block.TutorUserId is { } tutorUserId)
        {
            postings.Add(PostingDraft.Cr(AccountCodes.TutorPayable, allocation.TutorShare, SubLedgerOwnerTypes.Tutor, tutorUserId));
        }

        if (!allocation.PayeeNet.IsZero)
        {
            postings.Add(allocation.PayeeType == SubLedgerOwnerTypes.Organisation
                ? PostingDraft.Cr(AccountCodes.ProviderPayable, allocation.PayeeNet, SubLedgerOwnerTypes.Organisation, allocation.PayeeId)
                : PostingDraft.Cr(AccountCodes.TutorPayable, allocation.PayeeNet, SubLedgerOwnerTypes.Tutor, allocation.PayeeId));
        }

        return new JournalEntryDraft($"seat:{allocation.Id:N}", "seat_allocated", $"Seat {allocation.SeatNo} of {block.CohortName} allocated", postings, occurredAt);
    }

    /// <summary>Entry 16: open seats go back to the wallet. Dr 2020 (block) / Cr 2050 (wallet) the seats' value. The reference carries the running released count (or "closeout").</summary>
    public static JournalEntryDraft Entry16SeatsReleased(SeatBlock block, int seats, Money value, Guid walletId, DateTime occurredAt, string? referenceSuffix = null)
        => new(ReleaseReference(block.Id, referenceSuffix ?? (block.Released + seats).ToString(System.Globalization.CultureInfo.InvariantCulture)), "seat_block_release", $"{seats} seats released from {block.CohortName}",
            [PostingDraft.Dr(AccountCodes.ProgrammeFundsHeld, value, SubLedgerOwnerTypes.Programme, block.Id), PostingDraft.Cr(AccountCodes.UnitBalancesHeld, value, SubLedgerOwnerTypes.Wallet, walletId)], occurredAt);

    public static string PurchaseReference(Guid blockId) => $"seat-block:{blockId:N}:purchase";

    public static string ReleaseReference(Guid blockId, string suffix) => $"seat-block:{blockId:N}:release:{suffix}";

    /// <summary>Units released vs. price applied (as <c>PaymentPostings.AddRevaluation</c>): a surplus is revaluation income (Cr 4060), a shortfall a revaluation cost (Dr 4060).</summary>
    private static void AddRevaluation(List<PostingDraft> postings, Money released, Money applied)
    {
        var gap = released - applied;
        if (gap.IsZero)
        {
            return;
        }

        postings.Add(gap.IsNegative ? PostingDraft.Dr(AccountCodes.UnitRevaluation, new Money(-gap.AmountKobo, gap.Currency)) : PostingDraft.Cr(AccountCodes.UnitRevaluation, gap));
    }
}
