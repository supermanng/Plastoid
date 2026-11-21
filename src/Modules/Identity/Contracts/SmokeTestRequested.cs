using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Identity.Contracts;

/// <summary>
/// Diagnostics-only event used to prove the outbox → Kafka → inbox path end to end (Sprint 0).
/// Real Identity events (identity.user-registered.v1, identity.organisation-verified.v1) arrive in Sprint 1.
/// </summary>
[IntegrationEvent("identity", "smoke-test-requested", 1)]
public sealed record SmokeTestRequested(Guid AggregateId, string Note) : IIntegrationEvent;
