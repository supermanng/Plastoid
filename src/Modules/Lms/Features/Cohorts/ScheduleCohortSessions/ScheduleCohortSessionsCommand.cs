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

namespace Plastoids.Modules.Lms.Features.Cohorts.ScheduleCohortSessions;

/// <summary>Weekly live classes for a cohort: every listed weekday at the given UTC time, between the cohort's dates.</summary>
public sealed record ScheduleCohortSessionsCommand(Guid CohortId, IReadOnlyList<DayOfWeek> Weekdays, TimeOnly StartTimeUtc, int DurationMinutes, string TitlePrefix) : ICommand<ScheduleCohortSessionsResponse>;

public sealed record ScheduleCohortSessionsRequest(IReadOnlyList<DayOfWeek> Weekdays, TimeOnly StartTimeUtc, int DurationMinutes, string? TitlePrefix);

public sealed record ScheduleCohortSessionsResponse(Guid CohortId, int Created, DateTime? FirstSession, DateTime? LastSession);

internal sealed class ScheduleCohortSessionsValidator : AbstractValidator<ScheduleCohortSessionsCommand>
{
    public ScheduleCohortSessionsValidator()
    {
        RuleFor(x => x.CohortId).NotEmpty();
        RuleFor(x => x.Weekdays).NotEmpty();
        RuleFor(x => x.DurationMinutes).InclusiveBetween(15, 480);
        RuleFor(x => x.TitlePrefix).MaximumLength(120);
    }
}

internal sealed class ScheduleCohortSessionsHandler(LmsDbContext db, ILiveVideoProvider video, ICourseOutlineReader courses, TeachingAccess teaching, ICurrentUser currentUser) : ICommandHandler<ScheduleCohortSessionsCommand, ScheduleCohortSessionsResponse>
{
    public async Task<Result<ScheduleCohortSessionsResponse>> Handle(ScheduleCohortSessionsCommand request, CancellationToken cancellationToken)
    {
        var cohort = await db.Cohorts.SingleOrDefaultAsync(c => c.Id == request.CohortId, cancellationToken);
        if (cohort is null)
        {
            return Error.NotFound("cohort_not_found", "No such cohort.");
        }

        var userId = currentUser.UserId ?? Guid.Empty;
        if (cohort.TutorUserId != userId)
        {
            var course = await courses.GetPublishedAsync(cohort.CourseId, null, cancellationToken);
            if (course is null || !await teaching.CanManageCourseAsync(course, userId, cancellationToken))
            {
                return Error.Forbidden("not_cohort_tutor", "Only the cohort tutor, the course's teachers or staff can schedule sessions.");
            }
        }

        var learners = await db.Enrolments.AsNoTracking().Where(e => e.CohortId == cohort.Id).Select(e => e.LearnerId).ToListAsync(cancellationToken);
        var existing = await db.LiveSessions.AsNoTracking().Where(s => s.CohortId == cohort.Id).Select(s => s.StartsAt).ToHashSetAsync(cancellationToken);
        var created = 0;
        DateTime? first = null, last = null;
        var number = existing.Count;
        for (var day = cohort.StartsAt.Date; day <= cohort.EndsAt.Date; day = day.AddDays(1))
        {
            if (!request.Weekdays.Contains(day.DayOfWeek))
            {
                continue;
            }

            var startsAt = DateTime.SpecifyKind(day.Add(request.StartTimeUtc.ToTimeSpan()), DateTimeKind.Utc);
            if (startsAt < cohort.StartsAt || startsAt > cohort.EndsAt || existing.Contains(startsAt))
            {
                continue;
            }

            var id = Plastoids.SharedKernel.Domain.Entity.NewId();
            var endsAt = startsAt.AddMinutes(request.DurationMinutes);
            var room = await video.CreateRoomAsync(id, startsAt, endsAt, cancellationToken);
            if (room.IsFailure)
            {
                return Result.Failure<ScheduleCohortSessionsResponse>(room.Error);
            }

            number++;
            db.LiveSessions.Add(LiveSession.Schedule(cohort.TenantId, LiveSessionSources.Cohort, null, cohort.Id, cohort.TutorUserId, learners, $"{request.TitlePrefix} {number}".Trim(), startsAt, endsAt, video.Name, room.Value.RoomId, room.Value.RoomUrl.ToString()));
            created++;
            first ??= startsAt;
            last = startsAt;
        }

        return new ScheduleCohortSessionsResponse(cohort.Id, created, first, last);
    }
}

internal static class ScheduleCohortSessionsEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{cohortId:guid}/sessions", async (Guid cohortId, ScheduleCohortSessionsRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ScheduleCohortSessionsCommand(cohortId, body.Weekdays, body.StartTimeUtc, body.DurationMinutes, body.TitlePrefix ?? "Class"), ct)).ToHttpResult(StatusCodes.Status201Created))
            .WithName("ScheduleCohortSessions")
            .WithSummary("Teacher: create the cohort's weekly live classes (idempotent on start time).")
            .RequireAuthorization(Policies.Mfa); // cohort tutor or teaching access, decided in the handler (ADR 0019 §4)
    }
}
