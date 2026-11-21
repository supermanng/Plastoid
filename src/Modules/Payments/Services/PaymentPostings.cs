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

        // Credit refunds keep the cash: the learner's balance (2030) is the new liability. Card refunds leave client funds (or the receivable before settlement).
        postings.Add(refund.Destination == RefundDestinations.Credit
            ? PostingDraft.Cr(AccountCodes.LearnerRefundCredit, refund.Amount, SubLedgerOwnerTypes.Learner, refund.LearnerId ?? intent.PayerId)
            : PostingDraft.Cr(intent.SettledAt is null ? AccountCodes.GatewayReceivable : AccountCodes.BankClientFunds, refund.Amount));
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
}
