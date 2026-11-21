using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Identity.Contracts;

/// <summary>identity.user-registered.v1 (build prompt §2.8). PhoneHash is the blind index, never the number.</summary>
[IntegrationEvent("identity", "user-registered", 1)]
public sealed record UserRegistered(Guid AggregateId, Guid UserId, string PhoneHash, string Role) : IIntegrationEvent;
