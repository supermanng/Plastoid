using Plastoids.Infrastructure.Integrations;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payments.Domain;
using Plastoids.Modules.Payments.Persistence;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payments.Services;

/// <summary>Executes an approved refund: gateway call, ledger entry 5, intent update, event. Shared by issue (below threshold) and approve.</summary>
internal sealed class RefundIssuer(PaymentsDbContext db, IPaymentGateway gateway, ILedgerService ledger, IOutbox outbox, IClock clock)
{
    public async Task<Result> IssueAsync(PaymentIntent intent, Refund refund, CancellationToken ct)
    {
        var gatewayRef = await gateway.RefundAsync(intent.GatewayReference, refund.Amount, refund.Reason, ct);
        if (gatewayRef.IsFailure)
        {
            return gatewayRef.Error;
        }

        var now = clock.UtcNow;
        var entry = await ledger.PostAsync(PaymentPostings.Entry5Refund(intent, refund, now), ct);
        if (entry.IsFailure)
        {
            return entry.Error;
        }

        refund.MarkIssued(gatewayRef.Value, entry.Value, now);
        intent.ApplyRefund(refund.Amount);
        db.Transactions.Add(PaymentTransaction.Record(intent.TenantId, intent.Id, TransactionKinds.Refund, refund.Amount, Plastoids.SharedKernel.Domain.Money.Zero(refund.Amount.Currency), gatewayRef.Value, now, entry.Value));
        outbox.Enqueue(new RefundIssued(refund.Id, refund.Id, intent.Id, refund.Amount));
        return Result.Success();
    }
}
