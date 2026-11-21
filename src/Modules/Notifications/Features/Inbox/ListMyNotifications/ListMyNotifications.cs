using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Notifications.Domain;
using Plastoids.Modules.Notifications.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Notifications.Features.Inbox.ListMyNotifications;

/// <summary>In-app inbox (blueprint §9.16), newest first, cursor paged; each item shows its external delivery states.</summary>
public sealed record ListMyNotificationsQuery(string? Cursor, int? Limit, bool UnreadOnly) : IQuery<Page<NotificationView>>;

public sealed record DeliveryView(string Channel, string Status, DateTime? DeferredUntil, DateTime? SentAt, string? ProviderReference);

public sealed record NotificationView(Guid NotificationId, string TemplateKey, string? Subject, string Body, DateTime CreatedAt, DateTime? ReadAt, IReadOnlyList<DeliveryView> Deliveries);

public sealed record MarkReadCommand(Guid NotificationId) : ICommand<NotificationView>;

internal sealed class ListMyNotificationsValidator : AbstractValidator<ListMyNotificationsQuery>
{
    public ListMyNotificationsValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, Cursor.MaxLimit).When(x => x.Limit is not null);
    }
}

internal sealed class MarkReadValidator : AbstractValidator<MarkReadCommand>
{
    public MarkReadValidator()
    {
        RuleFor(x => x.NotificationId).NotEmpty();
    }
}

internal static class NotificationViews
{
    public static async Task<IReadOnlyList<NotificationView>> BuildAsync(NotificationsDbContext db, IReadOnlyList<Notification> items, CancellationToken ct)
    {
        var ids = items.Select(n => n.Id).ToList();
        var deliveries = await db.Deliveries.AsNoTracking().Where(d => ids.Contains(d.NotificationId)).ToListAsync(ct);
        return items.Select(n => new NotificationView(n.Id, n.TemplateKey, n.Subject, n.Body, n.CreatedAt, n.ReadAt,
            deliveries.Where(d => d.NotificationId == n.Id).Select(d => new DeliveryView(d.Channel, d.Status.ToString().ToLowerInvariant(), d.DeferredUntil, d.SentAt, d.ProviderReference)).ToList())).ToList();
    }
}

internal sealed class ListMyNotificationsHandler(NotificationsDbContext db, ICurrentUser currentUser) : IQueryHandler<ListMyNotificationsQuery, Page<NotificationView>>
{
    public async Task<Result<Page<NotificationView>>> Handle(ListMyNotificationsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? Guid.Empty;
        var limit = Cursor.ClampLimit(request.Limit);
        var query = db.Notifications.AsNoTracking().Where(n => n.RecipientId == userId);
        if (request.UnreadOnly)
        {
            query = query.Where(n => n.ReadAt == null);
        }

        if (Cursor.Decode(request.Cursor) is { } before)
        {
            query = query.Where(n => n.Id.CompareTo(before) < 0);
        }

        var rows = await query.OrderByDescending(n => n.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var page = rows.Take(limit).ToList();
        var views = await NotificationViews.BuildAsync(db, page, cancellationToken);
        return new Page<NotificationView>(views, rows.Count > limit ? Cursor.Encode(page[^1].Id) : null);
    }
}

internal sealed class MarkReadHandler(NotificationsDbContext db, ICurrentUser currentUser, IClock clock) : ICommandHandler<MarkReadCommand, NotificationView>
{
    public async Task<Result<NotificationView>> Handle(MarkReadCommand request, CancellationToken cancellationToken)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == request.NotificationId && n.RecipientId == currentUser.UserId, cancellationToken);
        if (notification is null)
        {
            return Error.NotFound("notification_not_found", "No such notification.");
        }

        notification.MarkRead(clock.UtcNow);
        return (await NotificationViews.BuildAsync(db, [notification], cancellationToken))[0];
    }
}

internal static class InboxEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/me", async (string? cursor, int? limit, bool? unreadOnly, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListMyNotificationsQuery(cursor, limit, unreadOnly ?? false), ct)).ToHttpResult())
            .WithName("ListMyNotifications")
            .WithSummary("In-app inbox, newest first (?unreadOnly=true, cursor paging).")
            .RequireAuthorization(Policies.Authenticated);

        group.MapPost("/{notificationId:guid}/read", async (Guid notificationId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new MarkReadCommand(notificationId), ct)).ToHttpResult())
            .WithName("MarkNotificationRead")
            .RequireAuthorization(Policies.Authenticated);
    }
}
