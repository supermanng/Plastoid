namespace Plastoids.Modules.Lms.Contracts;

public sealed record CohortInfo(Guid CohortId, Guid CourseId, Guid TutorUserId, string Name, IReadOnlyList<Guid> LearnerUserIds);

/// <summary>Who teaches and who sits in a cohort (Notifications: announcements fan out to members).</summary>
public interface ICohortMembers
{
    Task<CohortInfo?> GetAsync(Guid cohortId, CancellationToken cancellationToken);
}
