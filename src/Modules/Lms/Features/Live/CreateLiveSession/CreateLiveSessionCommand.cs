using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Lms.Domain;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Lms.Features.Live.CreateLiveSession;

/// <summary>Tutor/staff schedules a class for a cohort (bookings create their sessions automatically).</summary>
public sealed record CreateLiveSessionCommand(Guid? CohortId, string Title, DateTime StartsAt, int DurationMinutes, IReadOnlyList<Guid> ParticipantUserIds) : ICommand<CreateLiveSessionResponse>;

public sealed record CreateLiveSessionResponse(Guid LiveSessionId, string RoomId, Uri RoomUrl, DateTime StartsAt, DateTime EndsAt);

internal sealed class CreateLiveSessionValidator : AbstractValidator<CreateLiveSessionCommand>
{
    public CreateLiveSessionValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.DurationMinutes).InclusiveBetween(15, 480);
        RuleFor(x => x.ParticipantUserIds).NotNull().Must(p => p.Count <= 500);
    }
}

internal sealed class CreateLiveSessionHandler(LmsDbContext db, ILiveVideoProvider video, ICurrentUser currentUser, ICurrentTenant tenant, IClock clock) : ICommandHandler<CreateLiveSessionCommand, CreateLiveSessionResponse>
{
    public async Task<Result<CreateLiveSessionResponse>> Handle(CreateLiveSessionCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } hostId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        if (request.StartsAt <= clock.UtcNow)
        {
            return Error.Validation("starts_in_past", "The session must start in the future.");
        }

        var id = Plastoids.SharedKernel.Domain.Entity.NewId();
        var endsAt = request.StartsAt.AddMinutes(request.DurationMinutes);
        var room = await video.CreateRoomAsync(id, request.StartsAt, endsAt, cancellationToken);
        if (room.IsFailure)
        {
            return Result.Failure<CreateLiveSessionResponse>(room.Error);
        }

        var session = LiveSession.Schedule(tenant.TenantId, LiveSessionSources.Cohort, null, request.CohortId, hostId, request.ParticipantUserIds, request.Title, request.StartsAt, endsAt, video.Name, room.Value.RoomId, room.Value.RoomUrl.ToString());
        db.LiveSessions.Add(session);
        return new CreateLiveSessionResponse(session.Id, session.RoomId, room.Value.RoomUrl, session.StartsAt, session.EndsAt);
    }
}

internal static class CreateLiveSessionEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/", async (CreateLiveSessionCommand command, ISender sender, CancellationToken ct) => (await sender.Send(command, ct)).ToCreatedResult(r => $"/api/v1/lms/live-sessions/{r.LiveSessionId}"))
            .WithName("CreateLiveSession")
            .WithSummary("Tutor: schedule a live class (cohort). Booking sessions are created automatically on confirmation.")
            .RequireAuthorization(Policies.Tutor);
    }
}
