using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Lms.Domain;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.Modules.Lms.Services;
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

internal sealed class CreateLiveSessionHandler(LmsDbContext db, ILiveVideoProvider video, ICourseOutlineReader courses, TeachingAccess teaching, ICurrentUser currentUser, ICurrentTenant tenant, IClock clock) : ICommandHandler<CreateLiveSessionCommand, CreateLiveSessionResponse>
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

        // ADR 0019 §4: a cohort class may be scheduled by the cohort's tutor or anyone with teaching access to its course; the cohort tutor hosts it.
        if (request.CohortId is { } cohortId)
        {
            var cohort = await db.Cohorts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == cohortId, cancellationToken);
            if (cohort is null)
            {
                return Error.NotFound("cohort_not_found", "No such cohort.");
            }

            if (cohort.TutorUserId != hostId)
            {
                var course = await courses.GetPublishedAsync(cohort.CourseId, null, cancellationToken);
                if (course is null || !await teaching.CanManageCourseAsync(course, hostId, cancellationToken))
                {
                    return Error.Forbidden("not_cohort_tutor", "Only the cohort tutor, the course's teachers or staff can schedule its sessions.");
                }

                hostId = cohort.TutorUserId;
            }
        }
        else if (!currentUser.Roles.Contains("tutor") && !currentUser.Roles.Contains("staff"))
        {
            return Error.Forbidden("not_tutor", "Only tutors or staff can schedule a live session outside a cohort.");
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
            .WithSummary("Teacher: schedule a live class (cohort). Booking sessions are created automatically on confirmation.")
            .RequireAuthorization(Policies.Mfa); // cohort tutor or teaching access, decided in the handler (ADR 0019 §4)
    }
}
