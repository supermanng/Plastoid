namespace Plastoids.Modules.Payouts.Contracts;

/// <summary>An open hold. PayeeType "tutor" | "organisation" with PayeeId; TutorUserId is the payee id for tutors and null for organisations.</summary>
public sealed record DisputeHoldItem(Guid HoldId, Guid? TutorUserId, string Reason, Guid PlacedBy, DateTime PlacedAt, DateTime ReleasesAt, bool Overdue, string PayeeType = "tutor", Guid PayeeId = default);

/// <summary>Open dispute holds for the back-office disputes queue (blueprint §13 disputes, §18 holds).</summary>
public interface IDisputeQueue
{
    Task<IReadOnlyList<DisputeHoldItem>> ListOpenAsync(DateTime utcNow, int limit, CancellationToken cancellationToken);
}
