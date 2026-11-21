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

namespace Plastoids.Modules.Notifications.Features.Templates.UpsertTemplate;

/// <summary>Staff: add a new template version (unapproved) or approve one. Only approved versions are rendered; built-in copy is the fallback.</summary>
public sealed record UpsertTemplateCommand(string Key, string Channel, string? Subject, string Body, bool Approve, string IdempotencyKey) : ICommand<TemplateView>, IIdempotentCommand;

public sealed record UpsertTemplateRequest(string Key, string Channel, string? Subject, string Body, bool? Approve);

public sealed record ApproveTemplateCommand(Guid TemplateId, string IdempotencyKey) : ICommand<TemplateView>, IIdempotentCommand;

public sealed record ListTemplatesQuery(string? Key) : IQuery<IReadOnlyList<TemplateView>>;

public sealed record TemplateView(Guid TemplateId, string Key, string Channel, int Version, string? Subject, string Body, bool Approved, DateTime? ApprovedAt, string Preview)
{
    public static TemplateView From(NotificationTemplate t)
    {
        var sample = new Dictionary<string, string> { ["displayName"] = "Ada", ["courseTitle"] = "SQL for Analysts", ["amount"] = "NGN 36000.00", ["programme"] = "Data Analytics" };
        return new TemplateView(t.Id, t.Key, t.Channel, t.Version, t.Subject, t.Body, t.Approved, t.ApprovedAt, TemplateRenderer.Render(t.Subject, t.Body, sample).Body);
    }
}

internal sealed class UpsertTemplateValidator : AbstractValidator<UpsertTemplateCommand>
{
    public UpsertTemplateValidator()
    {
        RuleFor(x => x.Key).NotEmpty().MaximumLength(80).Matches("^[a-z0-9_.]+$");
        RuleFor(x => x.Channel).Must(c => c == "any" || c == Channels.InApp || Channels.IsExternal(c)).WithMessage("channel must be any, inapp, push, whatsapp, sms or email.");
        RuleFor(x => x.Subject).MaximumLength(200);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class ApproveTemplateValidator : AbstractValidator<ApproveTemplateCommand>
{
    public ApproveTemplateValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class ListTemplatesValidator : AbstractValidator<ListTemplatesQuery>
{
}

internal sealed class UpsertTemplateHandler(NotificationsDbContext db, ICurrentUser currentUser, ICurrentTenant tenant, IClock clock) : ICommandHandler<UpsertTemplateCommand, TemplateView>
{
    public async Task<Result<TemplateView>> Handle(UpsertTemplateCommand request, CancellationToken cancellationToken)
    {
        var latest = await db.Templates.Where(t => t.Key == request.Key && t.Channel == request.Channel).MaxAsync(t => (int?)t.Version, cancellationToken) ?? 0;
        var template = NotificationTemplate.Create(tenant.TenantId, request.Key, request.Channel, latest + 1, request.Subject, request.Body, currentUser.UserId ?? Guid.Empty);
        if (request.Approve)
        {
            template.Approve(currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        }

        db.Templates.Add(template);
        return TemplateView.From(template);
    }
}

internal sealed class ApproveTemplateHandler(NotificationsDbContext db, ICurrentUser currentUser, IClock clock) : ICommandHandler<ApproveTemplateCommand, TemplateView>
{
    public async Task<Result<TemplateView>> Handle(ApproveTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await db.Templates.SingleOrDefaultAsync(t => t.Id == request.TemplateId, cancellationToken);
        if (template is null)
        {
            return Error.NotFound("template_not_found", "No such template version.");
        }

        var approved = template.Approve(currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        return approved.IsFailure ? Result.Failure<TemplateView>(approved.Error) : TemplateView.From(template);
    }
}

internal sealed class ListTemplatesHandler(NotificationsDbContext db) : IQueryHandler<ListTemplatesQuery, IReadOnlyList<TemplateView>>
{
    public async Task<Result<IReadOnlyList<TemplateView>>> Handle(ListTemplatesQuery request, CancellationToken cancellationToken)
    {
        var query = db.Templates.AsNoTracking();
        if (request.Key is not null)
        {
            query = query.Where(t => t.Key == request.Key);
        }

        var rows = await query.OrderBy(t => t.Key).ThenBy(t => t.Channel).ThenByDescending(t => t.Version).Take(500).ToListAsync(cancellationToken);
        return rows.Select(TemplateView.From).ToList();
    }
}

internal static class TemplateEndpoints
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapGet("/templates", async (string? key, ISender sender, CancellationToken ct) => (await sender.Send(new ListTemplatesQuery(key), ct)).ToHttpResult())
            .WithName("ListNotificationTemplates")
            .RequireAuthorization(Policies.StaffAdmin);

        admin.MapPost("/templates", async (UpsertTemplateRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new UpsertTemplateCommand(body.Key, body.Channel, body.Subject, body.Body, body.Approve ?? false, key), ct)).ToCreatedResult(r => $"/api/v1/admin/notifications/templates/{r.TemplateId}");
            })
            .WithName("UpsertNotificationTemplate")
            .WithSummary("Staff: add a template version ({{placeholders}}); approve=true publishes it. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);

        admin.MapPost("/templates/{templateId:guid}/approve", async (Guid templateId, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new ApproveTemplateCommand(templateId, key), ct)).ToHttpResult();
            })
            .WithName("ApproveNotificationTemplate")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
