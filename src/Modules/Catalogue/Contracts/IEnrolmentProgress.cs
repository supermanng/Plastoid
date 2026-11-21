namespace Plastoids.Modules.Catalogue.Contracts;

/// <summary>
/// Review gate (blueprint §9.2: only verified enrolments review, after 30% progress). Lms provides the real
/// implementation in Sprint 5 through its Contracts; until then a stub answers from configuration/tests.
/// </summary>
public interface IEnrolmentProgress
{
    Task<EnrolmentProgressSnapshot?> GetAsync(Guid enrolmentId, Guid learnerId, Guid courseId, CancellationToken cancellationToken);
}

public sealed record EnrolmentProgressSnapshot(Guid EnrolmentId, Guid LearnerId, Guid CourseId, decimal ProgressPercent, bool IsVerified);
