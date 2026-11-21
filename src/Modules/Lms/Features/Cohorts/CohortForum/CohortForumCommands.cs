using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Lms.Domain;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Lms.Features.Cohorts.CohortForum;

public sealed record CreateThreadCommand(Guid CohortId, string Title, string Body) : ICommand<ThreadView>;

public sealed record CreateThreadRequest(string Title, string Body);

public sealed record PostReplyCommand(Guid ThreadId, string Body) : ICommand<PostView>;

public sealed record PostReplyRequest(string Body);

public sealed record ListThreadsQuery(Guid CohortId) : IQuery<IReadOnlyList<ThreadView>>;

public sealed record ThreadView(Guid ThreadId, Guid CohortId, Guid AuthorId, string Title, string Body, string Status, int PostCount, DateTime LastActivityAt);

public sealed record PostView(Guid PostId, Guid ThreadId, Guid AuthorId, string Body, bool Flagged, string? FlagReason, DateTime CreatedAt);

internal sealed class CreateThreadValidator : AbstractValidator<CreateThreadCommand>
{
    public CreateThreadValidator()
    {
        RuleFor(x => x.CohortId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}

internal sealed class PostReplyValidator : AbstractValidator<PostReplyCommand>
{
    public PostReplyValidator()
    {
        RuleFor(x => x.ThreadId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}

/// <summary>Members are the cohort's enrolled learners, its tutor and staff.</summary>
internal sealed class CohortMembership(LmsDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<Cohort>> RequireAsync(Guid cohortId, CancellationToken ct)
    {
        var cohort = await db.Cohorts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == cohortId, ct);
        if (cohort is null)
        {
            return Error.NotFound("cohort_not_found", "No such cohort.");
        }

        var userId = currentUser.UserId ?? Guid.Empty;
        var member = cohort.TutorUserId == userId || currentUser.Roles.Contains("staff") || await db.Enrolments.AsNoTracking().AnyAsync(e => e.CohortId == cohortId && e.LearnerId == userId, ct);
        return member ? cohort : Error.Forbidden("not_cohort_member", "Only cohort members can use this forum.");
    }
}

internal sealed class CreateThreadHandler(LmsDbContext db, CohortMembership membership, ICurrentUser currentUser, IClock clock) : ICommandHandler<CreateThreadCommand, ThreadView>
{
    public async Task<Result<ThreadView>> Handle(CreateThreadCommand request, CancellationToken cancellationToken)
    {
        var cohort = await membership.RequireAsync(request.CohortId, cancellationToken);
        if (cohort.IsFailure)
        {
            return Result.Failure<ThreadView>(cohort.Error);
        }

        var thread = ForumThread.Create(cohort.Value.TenantId, cohort.Value.Id, currentUser.UserId ?? Guid.Empty, request.Title, request.Body, clock.UtcNow);
        db.Threads.Add(thread);
        return Map(thread);
    }

    internal static ThreadView Map(ForumThread t) => new(t.Id, t.CohortId, t.AuthorId, t.Title, t.Body, t.Status.ToString().ToLowerInvariant(), t.PostCount, t.LastActivityAt);
}

internal sealed class PostReplyHandler(LmsDbContext db, CohortMembership membership, ICurrentUser currentUser, IClock clock) : ICommandHandler<PostReplyCommand, PostView>
{
    public async Task<Result<PostView>> Handle(PostReplyCommand request, CancellationToken cancellationToken)
    {
        var thread = await db.Threads.SingleOrDefaultAsync(t => t.Id == request.ThreadId, cancellationToken);
        if (thread is null)
        {
            return Error.NotFound("thread_not_found", "No such thread.");
        }

        var cohort = await membership.RequireAsync(thread.CohortId, cancellationToken);
        if (cohort.IsFailure)
        {
            return Result.Failure<PostView>(cohort.Error);
        }

        if (thread.Status == ThreadStatus.Locked)
        {
            return Error.Conflict("thread_locked", "This thread is locked.");
        }

        var now = clock.UtcNow;
        var post = ForumPost.Create(thread.TenantId, thread.Id, currentUser.UserId ?? Guid.Empty, request.Body, now);
        db.Posts.Add(post);
        thread.RecordPost(now);
        if (post.IsFlagged)
        {
            thread.Flag();
        }

        return new PostView(post.Id, post.ThreadId, post.AuthorId, post.Body, post.IsFlagged, post.FlagReason, now);
    }
}

internal sealed class ListThreadsHandler(LmsDbContext db, CohortMembership membership) : IQueryHandler<ListThreadsQuery, IReadOnlyList<ThreadView>>
{
    public async Task<Result<IReadOnlyList<ThreadView>>> Handle(ListThreadsQuery request, CancellationToken cancellationToken)
    {
        var cohort = await membership.RequireAsync(request.CohortId, cancellationToken);
        if (cohort.IsFailure)
        {
            return Result.Failure<IReadOnlyList<ThreadView>>(cohort.Error);
        }

        var threads = await db.Threads.AsNoTracking().Where(t => t.CohortId == request.CohortId).OrderByDescending(t => t.LastActivityAt).Take(200).ToListAsync(cancellationToken);
        return threads.Select(CreateThreadHandler.Map).ToList();
    }
}

internal static class CohortForumEndpoints
{
    public static void Map(IEndpointRouteBuilder lms)
    {
        lms.MapGet("/cohorts/{cohortId:guid}/threads", async (Guid cohortId, ISender sender, CancellationToken ct) => (await sender.Send(new ListThreadsQuery(cohortId), ct)).ToHttpResult())
            .WithName("ListCohortThreads")
            .RequireAuthorization(Policies.Authenticated);
        lms.MapPost("/cohorts/{cohortId:guid}/threads", async (Guid cohortId, CreateThreadRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateThreadCommand(cohortId, body.Title, body.Body), ct)).ToHttpResult(StatusCodes.Status201Created))
            .WithName("CreateCohortThread")
            .WithSummary("Cohort forum thread (posts are screened for contact details and off-platform payment requests).")
            .RequireAuthorization(Policies.Authenticated);
        lms.MapPost("/threads/{threadId:guid}/posts", async (Guid threadId, PostReplyRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new PostReplyCommand(threadId, body.Body), ct)).ToHttpResult(StatusCodes.Status201Created))
            .WithName("PostCohortReply")
            .RequireAuthorization(Policies.Authenticated);
    }
}
