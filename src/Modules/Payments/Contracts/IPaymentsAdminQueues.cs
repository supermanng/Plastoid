using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Payments.Contracts;

public sealed record PendingRefundItem(Guid RefundId, Guid PaymentIntentId, Money Amount, string Reason, Guid RequestedBy, DateTime RequestedAt, bool NeedsSecondApprover);

/// <summary>Back-office queue views over Payments (read-only; decisions stay on /admin/payments/*).</summary>
public interface IPaymentsAdminQueues
{
    Task<IReadOnlyList<PendingRefundItem>> ListPendingRefundsAsync(int limit, CancellationToken cancellationToken);
}
