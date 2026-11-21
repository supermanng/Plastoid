using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Identity.Features.Diagnostics.EmitSmokeEvent;

public sealed record EmitSmokeEventCommand(string Note) : ICommand<EmitSmokeEventResponse>;

public sealed record EmitSmokeEventResponse(Guid AggregateId);
