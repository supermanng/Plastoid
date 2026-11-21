using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Lms.Contracts;

public static class FundingSources
{
    public const string Self = "self";
    public const string Sponsored = "sponsored";
    public const string Employer = "employer";
}

/// <summary>lms.enrolment-created.v1 (build prompt §2.8).</summary>
[IntegrationEvent("lms", "enrolment-created", 1)]
public sealed record EnrolmentCreated(Guid AggregateId, Guid EnrolmentId, Guid LearnerId, Guid CourseId, Guid OfferingId, string FundingSource, Guid? FundingSourceId, Guid? CohortId) : IIntegrationEvent;

/// <summary>lms.enrolment-completed.v1: all required lessons done and pass marks met. Certificates issues on this.</summary>
[IntegrationEvent("lms", "enrolment-completed", 1)]
public sealed record EnrolmentCompleted(Guid AggregateId, Guid EnrolmentId, Guid LearnerId, Guid CourseId, Guid OfferingId, int VersionNo, decimal FinalScore, string FundingSource, Guid? FundingSourceId) : IIntegrationEvent;

/// <summary>lms.attempt-graded.v1: a quiz or exam attempt was scored.</summary>
[IntegrationEvent("lms", "attempt-graded", 1)]
public sealed record AttemptGraded(Guid AggregateId, Guid AttemptId, Guid EnrolmentId, Guid LessonId, decimal Score, decimal MaxScore, bool Passed) : IIntegrationEvent;
