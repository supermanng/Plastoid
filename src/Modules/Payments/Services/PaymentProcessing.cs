using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payments.Domain;
using Plastoids.Modules.Payments.Persistence;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payments.Services;

/// <summary>Shared by the webhook handler and the client-side verify: confirm/fail an intent exactly once and post the ledger entry.</summary>
internal sealed class PaymentProcessing(PaymentsDbContext db, ILedgerService ledger, IOutbox outbox, ICurrentTenant tenant, IClock clock)
{
    /// <summary>Idempotent: a second confirmation of the same intent is a no-op that still reports success.</summary>
    public async Task<Result<bool>> ConfirmAsync(PaymentIntent intent, Money gatewayFee, DateTime paidAt, CancellationToken ct)
    {
        if (intent.Status == PaymentStatus.Succeeded)
        {
            return false;
        }

        var entry = await ledger.PostAsync(intent.Purpose == PaymentPurposes.Units ? PaymentPostings.Entry11UnitPurchase(intent, paidAt) : PaymentPostings.Entry1Charge(intent, paidAt), ct);
        if (entry.IsFailure)
        {
            return Result.Failure<bool>(entry.Error);
        }

        intent.MarkSucceeded(gatewayFee, paidAt, entry.Value);
        db.Transactions.Add(PaymentTransaction.Record(intent.TenantId, intent.Id, TransactionKinds.Charge, intent.Amount, gatewayFee, intent.GatewayReference, paidAt, entry.Value));
        outbox.Enqueue(Succeeded(intent));
        return true;
    }

    /// <summary>A sale settled from the payer's unit wallet (ADR 0018): entry 12 instead of entry 1, no gateway, succeeded at once. Same event as a card payment, so enrolment, booking and earnings follow unchanged.</summary>
    public async Task<Result<bool>> ConfirmFromUnitsAsync(PaymentIntent intent, DateTime paidAt, CancellationToken ct)
    {
        if (intent.Status == PaymentStatus.Succeeded)
        {
            return false;
        }

        var entry = await ledger.PostAsync(PaymentPostings.Entry12UnitRedemption(intent, paidAt), ct);
        if (entry.IsFailure)
        {
            return Result.Failure<bool>(entry.Error);
        }

        intent.MarkSucceeded(Money.Zero(intent.Amount.Currency), paidAt, entry.Value);
        db.Transactions.Add(PaymentTransaction.Record(intent.TenantId, intent.Id, TransactionKinds.Charge, intent.Amount, Money.Zero(intent.Amount.Currency), intent.GatewayReference, paidAt, entry.Value));
        outbox.Enqueue(Succeeded(intent));
        return true;
    }

    private static PaymentSucceeded Succeeded(PaymentIntent intent)
        => new(intent.Id, intent.Id, intent.PayerId, intent.Purpose, intent.ReferenceId, intent.Amount, intent.PayeeUserId, intent.PayeeNet, intent.PayeeType, intent.PayeeOrganisationId, intent.TutorShare);

    public void Fail(PaymentIntent intent, string reason)
    {
        if (intent.MarkFailed(reason, clock.UtcNow))
        {
            outbox.Enqueue(new PaymentFailed(intent.Id, intent.Id, reason));
        }
    }

    /// <summary>Story G2: auto-match a virtual-account credit to its invoice; partial → flagged, over → held.</summary>
    public async Task<Result<string>> MatchTransferAsync(GatewayWebhook webhook, CancellationToken ct)
    {
        if (await db.Transfers.AnyAsync(t => t.GatewayReference == webhook.Reference, ct))
        {
            return "duplicate";
        }

        var invoice = webhook.VirtualAccountNumber is null ? null : await db.Invoices.SingleOrDefaultAsync(i => i.VirtualAccountNumber == webhook.VirtualAccountNumber, ct);
        if (invoice is null)
        {
            db.Transfers.Add(IncomingTransfer.Record(tenant.TenantId, webhook.Reference, webhook.VirtualAccountNumber ?? string.Empty, webhook.Amount, webhook.OccurredAt, null, "unmatched"));
            return "unmatched";
        }

        if (webhook.Amount > invoice.Outstanding)
        {
            invoice.ApplyTransfer(webhook.Amount, webhook.OccurredAt, Guid.Empty);
            db.Transfers.Add(IncomingTransfer.Record(invoice.TenantId, webhook.Reference, invoice.VirtualAccountNumber!, webhook.Amount, webhook.OccurredAt, invoice.Id, "held"));
            return "held";
        }

        var entry = await ledger.PostAsync(PaymentPostings.Entry6InvoicePaid(invoice, webhook.Amount, webhook.OccurredAt), ct);
        if (entry.IsFailure)
        {
            return Result.Failure<string>(entry.Error);
        }

        var match = invoice.ApplyTransfer(webhook.Amount, webhook.OccurredAt, entry.Value);
        if (match.IsFailure)
        {
            return Result.Failure<string>(match.Error);
        }

        var outcome = match.Value == TransferMatch.Matched ? "matched" : "partial";
        db.Transfers.Add(IncomingTransfer.Record(invoice.TenantId, webhook.Reference, invoice.VirtualAccountNumber!, webhook.Amount, webhook.OccurredAt, invoice.Id, outcome));
        if (match.Value == TransferMatch.Matched)
        {
            outbox.Enqueue(new InvoicePaid(invoice.Id, invoice.Id, invoice.OrganisationId, invoice.ProgrammeId, invoice.Total));
        }

        return outcome;
    }
}
