using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Sponsorship.Services;

/// <summary>Journal entries 7–10 of blueprint §18, drawn from programme funds held (2020, sub-ledger per programme).</summary>
internal static class SponsorshipPostings
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
