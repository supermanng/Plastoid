using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Notifications.Contracts;

/// <summary>notifications.message-flagged.v1: a thread message solicited off-platform payment or shared contact details (blueprint §19); Back office reviews it.</summary>
[IntegrationEvent("notifications", "message-flagged", 1)]
public sealed record MessageFlagged(Guid AggregateId, Guid ThreadId, Guid MessageId, Guid AuthorUserId, string Reason) : IIntegrationEvent;
