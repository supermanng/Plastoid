using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Tutors.Contracts;

/// <summary>tutors.tutor-verified.v1 (build prompt §2.8): the tutor passed identity, qualifications and subject review.</summary>
[IntegrationEvent("tutors", "tutor-verified", 1)]
public sealed record TutorVerified(Guid AggregateId, Guid TutorId, Guid UserId, string Level) : IIntegrationEvent;

/// <summary>tutors.review-sla-breached.v1: a reviewer-queue item passed its SLA (3 or 5 working days). Consumed by Notifications later.</summary>
[IntegrationEvent("tutors", "review-sla-breached", 1)]
public sealed record ReviewSlaBreached(Guid AggregateId, Guid QueueItemId, Guid TutorId, string Kind, DateTime DueAt) : IIntegrationEvent;
