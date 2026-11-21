using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Audit.Contracts;
using Plastoids.Modules.BackOffice.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.BackOffice.Features.AuditLog.ViewAuditLog;

/// <summary>Audit log viewer (blueprint §9.20) over Audit.Contracts; plus risk-alert resolution, which is itself audited.</summary>
public sealed record ViewAuditLogQuery(string? Cursor, int? Limit, Guid? ActorId, string? Entity, Guid? EntityId, DateTime? From, DateTime? To) : IQuery<AuditLogPage>;

public sealed record ResolveRiskAlertCommand(Guid AlertId, string Resolution, string IdempotencyKey) : ICommand<ResolveRiskAlertResponse>, IIdempotentCommand;

public sealed record ResolveRiskAlertRequest(string Resolution);

public sealed record ResolveRiskAlertResponse(Guid AlertId, string Kind, DateTime ResolvedAt, Guid ResolvedBy);

internal sealed class ViewAuditLogValidator : AbstractValidator<ViewAuditLogQuery>
{
    public ViewAuditLogValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, Cursor.MaxLimit).When(x => x.Limit is not null);
        RuleFor(x => x).Must(x => x.To > x.From).When(x => x.From is not null && x.To is not null).WithMessage("to must be after from.");
    }
}

internal sealed class ResolveRiskAlertValidator : AbstractValidator<ResolveRiskAlertCommand>
{
    public ResolveRiskAlertValidator()
    {
        RuleFor(x => x.AlertId).NotEmpty();
        RuleFor(x => x.Resolution).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class ViewAuditLogHandler(IAuditLogReader audit) : IQueryHandler<ViewAuditLogQuery, AuditLogPage>
{
    public async Task<Result<AuditLogPage>> Handle(ViewAuditLogQuery request, CancellationToken ct)
        => await audit.ListAsync(request.Cursor, request.Limit, request.ActorId, request.Entity, request.EntityId, request.From, request.To, ct);
}

internal sealed class ResolveRiskAlertHandler(BackOfficeDbContext db, ICurrentUser currentUser, IClock clock) : ICommandHandler<ResolveRiskAlertCommand, ResolveRiskAlertResponse>
{
    public async Task<Result<ResolveRiskAlertResponse>> Handle(ResolveRiskAlertCommand request, CancellationToken ct)
    {
        var alert = await db.Alerts.SingleOrDefaultAsync(a => a.Id == request.AlertId, ct);
        if (alert is null)
        {
            return Error.NotFound("alert_not_found", "No such risk alert.");
        }

        var resolved = alert.Resolve(currentUser.UserId ?? Guid.Empty, request.Resolution, clock.UtcNow);
        return resolved.IsFailure ? Result.Failure<ResolveRiskAlertResponse>(resolved.Error) : new ResolveRiskAlertResponse(alert.Id, alert.Kind, alert.ResolvedAt!.Value, alert.ResolvedBy!.Value);
    }
}

internal static class ViewAuditLogEndpoints
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapGet("/audit-log", async (string? cursor, int? limit, Guid? actorId, string? entity, Guid? entityId, DateTime? from, DateTime? to, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ViewAuditLogQuery(cursor, limit, actorId, entity, entityId, from, to), ct)).ToHttpResult())
            .WithName("BackOfficeAuditLog")
            .WithSummary("Staff: immutable audit log, newest first; filters actorId, entity, entityId, from, to; cursor-paged.")
            .RequireAuthorization(Policies.StaffAdmin);

        admin.MapPost("/risk-alerts/{alertId:guid}/resolve", async (Guid alertId, ResolveRiskAlertRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new ResolveRiskAlertCommand(alertId, body.Resolution, key), ct)).ToHttpResult();
            })
            .WithName("ResolveRiskAlert")
            .WithSummary("Staff: close a risk alert with the resolution (audited). Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
