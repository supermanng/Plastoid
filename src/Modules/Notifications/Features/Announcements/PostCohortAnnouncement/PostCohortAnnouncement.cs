using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Lms.Contracts;
using Plastoids.Modules.Notifications.Domain;
using Plastoids.Modules.Notifications.Persistence;
using Plastoids.Modules.Notifications.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Notifications.Features.Announcements.PostCohortAnnouncement;

/// <summary>Cohort announcement by its tutor (or staff): one inbox item per active learner, delivered by preference.</summary>
public sealed record PostCohortAnnouncementCommand(Guid CohortId, string Title, string Body) : ICommand<AnnouncementView>;

public sealed record PostCohortAnnouncementRequest(string Title, string Body);

public sealed record AnnouncementView(Guid AnnouncementId, Guid CohortId, string Title, int Recipients, DateTime PostedAt);

public sealed record ListCohortAnnouncementsQuery(Guid CohortId) : IQuery<IReadOnlyList<AnnouncementItem>>;

public sealed record AnnouncementItem(Guid AnnouncementId, string Title, string Body, Guid AuthorUserId, DateTime PostedAt);

internal sealed class PostCohortAnnouncementValidator : AbstractValidator<PostCohortAnnouncementCommand>
{
    public PostCohortAnnouncementValidator()
    {
        RuleFor(x => x.CohortId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}

internal sealed class ListCohortAnnouncementsValidator : AbstractValidator<ListCohortAnnouncementsQuery>
{
    public ListCohortAnnouncementsValidator()
    {
        RuleFor(x => x.CohortId).NotEmpty();
    }
}

internal sealed class PostCohortAnnouncementHandler(NotificationsDbContext db, ICohortMembers cohorts, NotificationDispatcher dispatcher, ICurrentUser currentUser, ICurrentTenant tenant, IClock clock) : ICommandHandler<PostCohortAnnouncementCommand, AnnouncementView>
{
    public async Task<Result<AnnouncementView>> Handle(PostCohortAnnouncementCommand request, CancellationToken cancellationToken)
    {
        var cohort = await cohorts.GetAsync(request.CohortId, cancellationToken);
        if (cohort is null)
        {
            return Error.NotFound("cohort_not_found", "No such cohort.");
        }

        var userId = currentUser.UserId ?? Guid.Empty;
        if (cohort.TutorUserId != userId && !currentUser.Roles.Contains("staff"))
        {
            return Error.Forbidden("not_cohort_tutor", "Only the cohort's tutor (or staff) can post announcements.");
        }

        var announcement = Announcement.Post(tenant.TenantId, cohort.CohortId, userId, request.Title, request.Body, cohort.LearnerUserIds.Count, clock.UtcNow);
        db.Announcements.Add(announcement);
        foreach (var learner in cohort.LearnerUserIds)
        {
            await dispatcher.DispatchAsync(learner, "notifications.cohort.announcement",
                new Dictionary<string, string> { ["announcementId"] = announcement.Id.ToString("D"), ["cohort"] = cohort.Name, ["title"] = announcement.Title, ["body"] = announcement.Body },
                "preferred", announcement.Id, cancellationToken);
        }

        return new AnnouncementView(announcement.Id, announcement.CohortId, announcement.Title, announcement.Recipients, announcement.PostedAt);
    }
}

internal sealed class ListCohortAnnouncementsHandler(NotificationsDbContext db, ICohortMembers cohorts, ICurrentUser currentUser) : IQueryHandler<ListCohortAnnouncementsQuery, IReadOnlyList<AnnouncementItem>>
{
    public async Task<Result<IReadOnlyList<AnnouncementItem>>> Handle(ListCohortAnnouncementsQuery request, CancellationToken cancellationToken)
    {
        var cohort = await cohorts.GetAsync(request.CohortId, cancellationToken);
        var userId = currentUser.UserId ?? Guid.Empty;
        if (cohort is null || (cohort.TutorUserId != userId && !cohort.LearnerUserIds.Contains(userId) && !currentUser.Roles.Contains("staff")))
        {
            return Error.Forbidden("not_cohort_member", "Only cohort members see its announcements.");
        }

        var rows = await db.Announcements.AsNoTracking().Where(a => a.CohortId == cohort.CohortId).OrderByDescending(a => a.PostedAt).Take(100).ToListAsync(cancellationToken);
        return rows.Select(a => new AnnouncementItem(a.Id, a.Title, a.Body, a.AuthorUserId, a.PostedAt)).ToList();
    }
}

internal static class CohortAnnouncementEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/cohorts/{cohortId:guid}/announcements", async (Guid cohortId, PostCohortAnnouncementRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new PostCohortAnnouncementCommand(cohortId, body.Title, body.Body), ct)).ToCreatedResult(r => $"/api/v1/notifications/cohorts/{r.CohortId}/announcements"))
            .WithName("PostCohortAnnouncement")
            .WithSummary("Tutor: announce to every active learner in the cohort.")
            .RequireAuthorization(Policies.Authenticated);

        group.MapGet("/cohorts/{cohortId:guid}/announcements", async (Guid cohortId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListCohortAnnouncementsQuery(cohortId), ct)).ToHttpResult())
            .WithName("ListCohortAnnouncements")
            .RequireAuthorization(Policies.Authenticated);
    }
}
