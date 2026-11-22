using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payments.Domain;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Payments.Services;

/// <summary>The journal entries of blueprint §18, built from payment rows. Numbers in names refer to the illustrative entries.</summary>
internal static class PaymentPostings
{
    private static string RevenueAccount(PaymentIntent intent) => intent.Purpose == PaymentPurposes.Booking ? AccountCodes.TutoringCommission : AccountCodes.CourseCommission;

    /// <summary>Entry 1: learner pays by card. Dr 1030 gross; Cr 2010 (tutor) net; Cr 4010/4020 commission. Co-pay credits 2020 (programme).</summary>
    public static JournalEntryDraft Entry1Charge(PaymentIntent intent, DateTime occurredAt)
    {
        var postings = new List<PostingDraft> { PostingDraft.Dr(AccountCodes.GatewayReceivable, intent.Amount) };
        if (intent.Purpose == PaymentPurposes.CoPay)
        {
            postings.Add(PostingDraft.Cr(AccountCodes.ProgrammeFundsHeld, intent.Amount, SubLedgerOwnerTypes.Programme, intent.ReferenceId));
        }
        else
        {
            if (!intent.PayeeNet.IsZero)
            {
                postings.Add(PostingDraft.Cr(AccountCodes.TutorPayable, intent.PayeeNet, SubLedgerOwnerTypes.Tutor, intent.PayeeUserId));
            }

            if (!intent.Commission.IsZero)
            {
                postings.Add(PostingDraft.Cr(RevenueAccount(intent), intent.Commission));
            }
        }

        return new JournalEntryDraft($"payment:{intent.Id:N}:charge", "payment_charge", $"{intent.Purpose} payment {intent.GatewayReference}", postings, occurredAt);
    }

    /// <summary>Entry 11 (ADR 0018): units bought by card/transfer. Dr 1030 gross; Cr 2050 (wallet) the same: cash held against the unit balance, no revenue yet.</summary>
    public static JournalEntryDraft Entry11UnitPurchase(PaymentIntent intent, DateTime occurredAt)
        => new($"payment:{intent.Id:N}:charge", "unit_purchase", $"Unit purchase {intent.GatewayReference}",
            [PostingDraft.Dr(AccountCodes.GatewayReceivable, intent.Amount), PostingDraft.Cr(AccountCodes.UnitBalancesHeld, intent.Amount, SubLedgerOwnerTypes.Wallet, intent.ReferenceId)], occurredAt);

    /// <summary>
    /// Entry 12 (ADR 0018): a sale paid with units. Dr 2050 (wallet) the naira the units were bought for; Cr tutor payable / commission (or programme
    /// funds for a co-pay) on the sale price; any gap between the two goes to 4060 (units bought at another rate, or whole-unit rounding).
    /// </summary>
    public static JournalEntryDraft Entry12UnitRedemption(PaymentIntent intent, DateTime occurredAt)
    {
        var unitsValue = new Money(intent.UnitsValueKobo, intent.Amount.Currency);
        var postings = new List<PostingDraft> { PostingDraft.Dr(AccountCodes.UnitBalancesHeld, unitsValue, SubLedgerOwnerTypes.Wallet, intent.UnitsWalletId) };
        if (intent.Purpose == PaymentPurposes.CoPay)
        {
            postings.Add(PostingDraft.Cr(AccountCodes.ProgrammeFundsHeld, intent.Amount, SubLedgerOwnerTypes.Programme, intent.ReferenceId));
        }
        else
        {
            if (!intent.PayeeNet.IsZero)
            {
                postings.Add(PostingDraft.Cr(AccountCodes.TutorPayable, intent.PayeeNet, SubLedgerOwnerTypes.Tutor, intent.PayeeUserId));
            }

            if (!intent.Commission.IsZero)
            {
                postings.Add(PostingDraft.Cr(RevenueAccount(intent), intent.Commission));
            }
        }

        AddRevaluation(postings, unitsValue, intent.Amount);
        return new JournalEntryDraft($"payment:{intent.Id:N}:charge", "unit_redemption", $"{intent.Purpose} paid with {intent.UnitsCharged} units", postings, occurredAt);
    }

    /// <summary>Units released vs. price applied: a surplus is revaluation income (Cr 4060), a shortfall a revaluation cost (Dr 4060).</summary>
    private static void AddRevaluation(List<PostingDraft> postings, Money released, Money applied)
    {
        var gap = released - applied;
        if (gap.IsZero)
        {
            return;
        }

        postings.Add(gap.IsNegative ? PostingDraft.Dr(AccountCodes.UnitRevaluation, new Money(-gap.AmountKobo, gap.Currency)) : PostingDraft.Cr(AccountCodes.UnitRevaluation, gap));
    }

    /// <summary>Entry 2: gateway settles net of its fee. Dr 1020 net, Dr 5010 fee; Cr 1030 gross.</summary>
    public static JournalEntryDraft Entry2Settlement(PaymentIntent intent, Money fee, DateTime settledAt)
    {
        var postings = new List<PostingDraft> { PostingDraft.Dr(AccountCodes.BankClientFunds, intent.Amount - fee) };
        if (!fee.IsZero)
        {
            postings.Add(PostingDraft.Dr(AccountCodes.ProcessingFees, fee));
        }

        postings.Add(PostingDraft.Cr(AccountCodes.GatewayReceivable, intent.Amount));
        return new JournalEntryDraft($"payment:{intent.Id:N}:settlement", "gateway_settlement", $"Settlement of {intent.GatewayReference}", postings, settledAt);
    }

    /// <summary>Entry 3: earned commission (net of gateway fees) swept from client funds to operating.</summary>
    public static JournalEntryDraft Entry3Sweep(string batchReference, Money amount, DateTime occurredAt)
        => new($"sweep:{batchReference}", "revenue_sweep", "Earned commission swept to operating account",
            [PostingDraft.Dr(AccountCodes.BankOperating, amount), PostingDraft.Cr(AccountCodes.BankClientFunds, amount)], occurredAt);

    /// <summary>Entry 5: refund reverses the tutor's and Plastoids' shares pro rata; cash leaves client funds (or the receivable if not yet settled).</summary>
    public static JournalEntryDraft Entry5Refund(PaymentIntent intent, Refund refund, DateTime occurredAt)
    {
        var fraction = (decimal)refund.Amount.AmountKobo / intent.Amount.AmountKobo;
        var commissionShare = new Money((long)Math.Round(intent.Commission.AmountKobo * fraction, 0, MidpointRounding.ToEven), intent.Amount.Currency);
        var tutorShare = refund.Amount - commissionShare;
        var postings = new List<PostingDraft>();
        if (intent.Purpose == PaymentPurposes.CoPay)
        {
            postings.Add(PostingDraft.Dr(AccountCodes.ProgrammeFundsHeld, refund.Amount, SubLedgerOwnerTypes.Programme, intent.ReferenceId));
        }
        else
        {
            if (!tutorShare.IsZero)
            {
                postings.Add(PostingDraft.Dr(AccountCodes.TutorPayable, tutorShare, SubLedgerOwnerTypes.Tutor, intent.PayeeUserId));
            }

            if (!commissionShare.IsZero)
            {
                postings.Add(PostingDraft.Dr(RevenueAccount(intent), commissionShare));
            }
        }

        // Credit refunds keep the cash: the learner's balance (2030) is the new liability. Unit refunds restore the unit liability (2050) for the
        // same wallet. Card refunds leave client funds (or the receivable before settlement).
        postings.Add(refund.Destination switch
        {
            RefundDestinations.Credit => PostingDraft.Cr(AccountCodes.LearnerRefundCredit, refund.Amount, SubLedgerOwnerTypes.Learner, refund.LearnerId ?? intent.PayerId),
            RefundDestinations.Units => PostingDraft.Cr(AccountCodes.UnitBalancesHeld, refund.Amount, SubLedgerOwnerTypes.Wallet, intent.UnitsWalletId),
            _ => PostingDraft.Cr(intent.SettledAt is null ? AccountCodes.GatewayReceivable : AccountCodes.BankClientFunds, refund.Amount),
        });
        return new JournalEntryDraft($"refund:{refund.Id:N}", "refund", $"Refund of {intent.GatewayReference}: {refund.Reason}", postings, occurredAt);
    }

    /// <summary>Entry 6: sponsor pays an invoice. Dr 1020 amount; Cr 2020 (programme) the funds part; Cr 2040 the VAT part (VAT is credited last on partial payments).</summary>
    public static JournalEntryDraft Entry6InvoicePaid(Invoice invoice, Money amountReceived, DateTime occurredAt)
    {
        var currency = amountReceived.Currency;
        var alreadyPaid = invoice.PaidAmount.AmountKobo;
        var fundsRemaining = Math.Max(0, invoice.Subtotal.AmountKobo - alreadyPaid);
        var fundsPortionKobo = Math.Min(amountReceived.AmountKobo, fundsRemaining);
        var vatPortionKobo = amountReceived.AmountKobo - fundsPortionKobo;

        var postings = new List<PostingDraft> { PostingDraft.Dr(AccountCodes.BankClientFunds, amountReceived) };
        if (fundsPortionKobo > 0)
        {
            postings.Add(PostingDraft.Cr(AccountCodes.ProgrammeFundsHeld, new Money(fundsPortionKobo, currency), SubLedgerOwnerTypes.Programme, invoice.ProgrammeId ?? invoice.OrganisationId));
        }

        if (vatPortionKobo > 0)
        {
            postings.Add(PostingDraft.Cr(AccountCodes.VatPayable, new Money(vatPortionKobo, currency)));
        }

        return new JournalEntryDraft($"invoice:{invoice.Id:N}:{alreadyPaid + amountReceived.AmountKobo}", "invoice_paid", $"Invoice {invoice.Number} paid", postings, occurredAt);
    }

    /// <summary>Entry 6 paid with units (ADR 0018): Dr 2050 (wallet) the naira released; Cr 2020 / 2040 as for a transfer; the gap to 4060.</summary>
    public static JournalEntryDraft Entry6InvoicePaidFromUnits(Invoice invoice, Money amountApplied, Money unitsValue, Guid walletId, DateTime occurredAt)
    {
        var currency = amountApplied.Currency;
        var alreadyPaid = invoice.PaidAmount.AmountKobo;
        var fundsRemaining = Math.Max(0, invoice.Subtotal.AmountKobo - alreadyPaid);
        var fundsPortionKobo = Math.Min(amountApplied.AmountKobo, fundsRemaining);
        var vatPortionKobo = amountApplied.AmountKobo - fundsPortionKobo;

        var postings = new List<PostingDraft> { PostingDraft.Dr(AccountCodes.UnitBalancesHeld, unitsValue, SubLedgerOwnerTypes.Wallet, walletId) };
        if (fundsPortionKobo > 0)
        {
            postings.Add(PostingDraft.Cr(AccountCodes.ProgrammeFundsHeld, new Money(fundsPortionKobo, currency), SubLedgerOwnerTypes.Programme, invoice.ProgrammeId ?? invoice.OrganisationId));
        }

        if (vatPortionKobo > 0)
        {
            postings.Add(PostingDraft.Cr(AccountCodes.VatPayable, new Money(vatPortionKobo, currency)));
        }

        AddRevaluation(postings, unitsValue, amountApplied);
        return new JournalEntryDraft($"invoice:{invoice.Id:N}:units:{alreadyPaid + amountApplied.AmountKobo}", "invoice_paid_units", $"Invoice {invoice.Number} paid with units", postings, occurredAt);
    }
}
