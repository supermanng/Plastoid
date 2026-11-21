using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Applications.Contracts;

/// <summary>applications.offer-accepted.v1 (build prompt §2.8): the learner accepted a sponsored seat; Lms enrols them, Sponsorship fills the seat.</summary>
[IntegrationEvent("applications", "offer-accepted", 1)]
public sealed record OfferAccepted(Guid AggregateId, Guid ApplicationId, Guid ProgrammeId, Guid LearnerId, Guid OfferingId, Guid SeatId) : IIntegrationEvent;

/// <summary>applications.decision-recorded.v1: accepted | rejected | waitlisted, by whom.</summary>
[IntegrationEvent("applications", "decision-recorded", 1)]
public sealed record DecisionRecorded(Guid AggregateId, Guid ApplicationId, Guid ProgrammeId, Guid LearnerId, string Decision, Guid DecidedBy) : IIntegrationEvent;
