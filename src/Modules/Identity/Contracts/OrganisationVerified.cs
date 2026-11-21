using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Identity.Contracts;

/// <summary>identity.organisation-verified.v1 (build prompt §2.8).</summary>
[IntegrationEvent("identity", "organisation-verified", 1)]
public sealed record OrganisationVerified(Guid AggregateId, Guid OrganisationId, string Type) : IIntegrationEvent;
