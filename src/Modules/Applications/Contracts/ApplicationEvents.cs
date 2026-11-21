using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Applications.Contracts;

/// <summary>applications.application-submitted.v1: feeds the programme funnel ("applied").</summary>
[IntegrationEvent("applications", "application-submitted", 1)]
public sealed record ApplicationSubmitted(Guid AggregateId, Guid ApplicationId, Guid ProgrammeId, Guid LearnerId, bool Flagged) : IIntegrationEvent;

/// <summary>applications.decision-recorded.v1: accepted | rejected | waitlisted, by whom.</summary>
[IntegrationEvent("applications", "decision-recorded", 1)]
public sealed record DecisionRecorded(Guid AggregateId, Guid ApplicationId, Guid ProgrammeId, Guid LearnerId, string Decision, Guid DecidedBy) : IIntegrationEvent;

/// <summary>applications.offer-sent.v1: a seat was reserved and the offer letter sent (5-day expiry).</summary>
[IntegrationEvent("applications", "offer-sent", 1)]
public sealed record OfferSent(Guid AggregateId, Guid ApplicationId, Guid ProgrammeId, Guid LearnerId, Guid SeatId, DateTime ExpiresAt) : IIntegrationEvent;

/// <summary>applications.offer-accepted.v1 (build prompt §2.8): the learner accepted a sponsored seat (co-pay settled when required); Lms enrols them, Sponsorship fills the seat.</summary>
[IntegrationEvent("applications", "offer-accepted", 1)]
public sealed record OfferAccepted(Guid AggregateId, Guid ApplicationId, Guid ProgrammeId, Guid LearnerId, Guid OfferingId, Guid SeatId) : IIntegrationEvent;
