using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Identity.Contracts;

/// <summary>identity.user-registered.v1 (build prompt §2.8). PhoneHash is the blind index, never the number; Email (lower-case) is set for email registrations so pending seat invites can match (ADR 0021).</summary>
[IntegrationEvent("identity", "user-registered", 1)]
public sealed record UserRegistered(Guid AggregateId, Guid UserId, string PhoneHash, string Role, string? Email = null) : IIntegrationEvent;

/// <summary>identity.email-linked.v1 (ADR 0021): an existing account linked an email address (lower-case). Sponsorship converts seat invites addressed to that email.</summary>
[IntegrationEvent("identity", "email-linked", 1)]
public sealed record EmailLinked(Guid AggregateId, Guid UserId, string Email) : IIntegrationEvent;
