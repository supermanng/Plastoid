using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Notifications.Contracts;
using Plastoids.Modules.Notifications.Domain;
using Plastoids.Modules.Notifications.Persistence;
using Plastoids.Modules.Notifications.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Notifications.Features.Threads.MessageThreads;

/// <summary>Learner–tutor messaging stays on platform and is retained; messages are scanned and flagged (blueprint §19), never dropped.</summary>
public sealed record OpenThreadCommand(Guid OtherUserId, string Subject, string Body, Guid? ContextId) : ICommand<ThreadView>;

public sealed record PostMessageCommand(Guid ThreadId, string Body) : ICommand<MessageView>;

public sealed record PostMessageRequest(string Body);

public sealed record ListMyThreadsQuery : IQuery<IReadOnlyList<ThreadView>>;

public sealed record GetThreadQuery(Guid ThreadId) : IQuery<ThreadDetailView>;

public sealed record ThreadView(Guid ThreadId, Guid LearnerUserId, Guid TutorUserId, string Subject, DateTime LastMessageAt, int MessageCount, bool Flagged);

public sealed record MessageView(Guid MessageId, Guid ThreadId, Guid AuthorUserId, string Body, DateTime SentAt, bool Flagged, string? FlagReason);

public sealed record ThreadDetailView(ThreadView Thread, IReadOnlyList<MessageView> Messages);

internal sealed class OpenThreadValidator : AbstractValidator<OpenThreadCommand>
{
    public OpenThreadValidator()
    {
        RuleFor(x => x.OtherUserId).NotEmpty();
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}

internal sealed class PostMessageValidator : AbstractValidator<PostMessageCommand>
{
    public PostMessageValidator()
    {
        RuleFor(x => x.ThreadId).NotEmpty();
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
    }
}

internal sealed class ListMyThreadsValidator : AbstractValidator<ListMyThreadsQuery>
{
}

internal sealed class GetThreadValidator : AbstractValidator<GetThreadQuery>
{
    public GetThreadValidator()
    {
        RuleFor(x => x.ThreadId).NotEmpty();
    }
}

internal static class ThreadViews
{
    public static ThreadView Of(MessageThread t) => new(t.Id, t.LearnerUserId, t.TutorUserId, t.Subject, t.LastMessageAt, t.MessageCount, t.IsFlagged);
    public static MessageView Of(ThreadMessage m) => new(m.Id, m.ThreadId, m.AuthorUserId, m.Body, m.SentAt, m.IsFlagged, m.FlagReason);
}

internal sealed class OpenThreadHandler(NotificationsDbContext db, IUserDirectory users, NotificationDispatcher dispatcher, IOutbox outbox, ICurrentUser currentUser, ICurrentTenant tenant, IClock clock) : ICommandHandler<OpenThreadCommand, ThreadView>
{
    public async Task<Result<ThreadView>> Handle(OpenThreadCommand request, CancellationToken cancellationToken)
    {
        var me = currentUser.UserId ?? Guid.Empty;
        if (request.OtherUserId == me)
        {
            return Error.Validation("thread_self", "You cannot message yourself.");
        }

        var other = await users.GetAsync(request.OtherUserId, cancellationToken);
        if (other is null || !other.IsActive)
        {
            return Error.NotFound("user_not_found", "No such user.");
        }

        var iAmTutor = currentUser.Roles.Contains("tutor");
        var now = clock.UtcNow;
        var thread = iAmTutor
            ? MessageThread.Open(tenant.TenantId, request.OtherUserId, me, request.Subject, request.ContextId, now)
            : MessageThread.Open(tenant.TenantId, me, request.OtherUserId, request.Subject, request.ContextId, now);
        db.Threads.Add(thread);
        var message = thread.Post(me, request.Body, now);
        db.Messages.Add(message);
        await NotifyAsync(thread, message, me, dispatcher, outbox, users, cancellationToken);
        return ThreadViews.Of(thread);
    }

    internal static async Task NotifyAsync(MessageThread thread, ThreadMessage message, Guid author, NotificationDispatcher dispatcher, IOutbox outbox, IUserDirectory users, CancellationToken ct)
    {
        var sender = await users.GetAsync(author, ct);
        var preview = message.Body.Length > 80 ? message.Body[..80] + "…" : message.Body;
        await dispatcher.DispatchAsync(thread.OtherParty(author), "notifications.thread.message",
            new Dictionary<string, string> { ["threadId"] = thread.Id.ToString("D"), ["messageId"] = message.Id.ToString("D"), ["senderName"] = sender?.DisplayName ?? "Someone", ["preview"] = preview }, "preferred", message.Id, ct);
        if (message.IsFlagged)
        {
            outbox.Enqueue(new MessageFlagged(thread.Id, thread.Id, message.Id, author, message.FlagReason!));
        }
    }
}

internal sealed class PostMessageHandler(NotificationsDbContext db, IUserDirectory users, NotificationDispatcher dispatcher, IOutbox outbox, ICurrentUser currentUser, IClock clock) : ICommandHandler<PostMessageCommand, MessageView>
{
    public async Task<Result<MessageView>> Handle(PostMessageCommand request, CancellationToken cancellationToken)
    {
        var me = currentUser.UserId ?? Guid.Empty;
        var thread = await db.Threads.SingleOrDefaultAsync(t => t.Id == request.ThreadId, cancellationToken);
        if (thread is null || !thread.IsParticipant(me))
        {
            return Error.NotFound("thread_not_found", "No such thread.");
        }

        var message = thread.Post(me, request.Body, clock.UtcNow);
        db.Messages.Add(message);
        await OpenThreadHandler.NotifyAsync(thread, message, me, dispatcher, outbox, users, cancellationToken);
        return ThreadViews.Of(message);
    }
}

internal sealed class ListMyThreadsHandler(NotificationsDbContext db, ICurrentUser currentUser) : IQueryHandler<ListMyThreadsQuery, IReadOnlyList<ThreadView>>
{
    public async Task<Result<IReadOnlyList<ThreadView>>> Handle(ListMyThreadsQuery request, CancellationToken cancellationToken)
    {
        var me = currentUser.UserId ?? Guid.Empty;
        var rows = await db.Threads.AsNoTracking().Where(t => t.LearnerUserId == me || t.TutorUserId == me).OrderByDescending(t => t.LastMessageAt).Take(100).ToListAsync(cancellationToken);
        return rows.Select(ThreadViews.Of).ToList();
    }
}

internal sealed class GetThreadHandler(NotificationsDbContext db, ICurrentUser currentUser) : IQueryHandler<GetThreadQuery, ThreadDetailView>
{
    public async Task<Result<ThreadDetailView>> Handle(GetThreadQuery request, CancellationToken cancellationToken)
    {
        var me = currentUser.UserId ?? Guid.Empty;
        var thread = await db.Threads.AsNoTracking().SingleOrDefaultAsync(t => t.Id == request.ThreadId, cancellationToken);
        if (thread is null || (!thread.IsParticipant(me) && !currentUser.Roles.Contains("staff")))
        {
            return Error.NotFound("thread_not_found", "No such thread.");
        }

        var messages = await db.Messages.AsNoTracking().Where(m => m.ThreadId == thread.Id).OrderBy(m => m.SentAt).ToListAsync(cancellationToken);
        return new ThreadDetailView(ThreadViews.Of(thread), messages.Select(ThreadViews.Of).ToList());
    }
}

internal static class ThreadEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/threads", async (OpenThreadCommand body, ISender sender, CancellationToken ct) => (await sender.Send(body, ct)).ToCreatedResult(r => $"/api/v1/notifications/threads/{r.ThreadId}"))
            .WithName("OpenThread")
            .WithSummary("Open a learner–tutor thread with a first message (kept on platform; scanned for contact details and off-platform payment).")
            .RequireAuthorization(Policies.Authenticated);

        group.MapGet("/threads", async (ISender sender, CancellationToken ct) => (await sender.Send(new ListMyThreadsQuery(), ct)).ToHttpResult())
            .WithName("ListMyThreads")
            .RequireAuthorization(Policies.Authenticated);

        group.MapGet("/threads/{threadId:guid}", async (Guid threadId, ISender sender, CancellationToken ct) => (await sender.Send(new GetThreadQuery(threadId), ct)).ToHttpResult())
            .WithName("GetThread")
            .RequireAuthorization(Policies.Authenticated);

        group.MapPost("/threads/{threadId:guid}/messages", async (Guid threadId, PostMessageRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new PostMessageCommand(threadId, body.Body), ct)).ToHttpResult(StatusCodes.Status201Created))
            .WithName("PostThreadMessage")
            .RequireAuthorization(Policies.Authenticated);
    }
}
