namespace Plastoids.Modules.Payouts.Contracts;

public sealed record DisputeHoldItem(Guid HoldId, Guid TutorUserId, string Reason, Guid PlacedBy, DateTime PlacedAt, DateTime ReleasesAt, bool Overdue);

/// <summary>Open dispute holds for the back-office disputes queue (blueprint §13 disputes, §18 holds).</summary>
public interface IDisputeQueue
{
    Task<IReadOnlyList<DisputeHoldItem>> ListOpenAsync(DateTime utcNow, int limit, CancellationToken cancellationToken);
}
