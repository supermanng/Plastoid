using Plastoids.Modules.Identity.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Identity.Features.Diagnostics.EmitSmokeEvent;

internal sealed class EmitSmokeEventHandler(IOutbox outbox) : ICommandHandler<EmitSmokeEventCommand, EmitSmokeEventResponse>
{
    public Task<Result<EmitSmokeEventResponse>> Handle(EmitSmokeEventCommand request, CancellationToken cancellationToken)
    {
        var aggregateId = Entity.NewId();
        outbox.Enqueue(new SmokeTestRequested(aggregateId, request.Note));
        return Task.FromResult(Result.Success(new EmitSmokeEventResponse(aggregateId)));
    }
}
