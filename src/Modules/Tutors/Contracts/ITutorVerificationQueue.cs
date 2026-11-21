namespace Plastoids.Modules.Tutors.Contracts;

public sealed record TutorVerificationItem(Guid QueueItemId, Guid TutorId, Guid TutorUserId, string Kind, Guid ReferenceId, DateTime EnqueuedAt, DateTime DueAt, bool Overdue, string TutorStatus);

public sealed record TutorAdminSummary(Guid TutorId, Guid UserId, string Status, string Level, bool IdentityPassed, decimal QualityScore, int SessionsDelivered, DateTime? VerifiedAt);

/// <summary>Back-office view of pending tutor verifications (blueprint §13 stages) and tutor status.</summary>
public interface ITutorVerificationQueue
{
    Task<IReadOnlyList<TutorVerificationItem>> ListPendingAsync(DateTime utcNow, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<TutorAdminSummary>> ListTutorsAsync(string? status, int limit, CancellationToken cancellationToken);
}
