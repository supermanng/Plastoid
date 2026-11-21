using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Infrastructure.Security;
using Plastoids.Infrastructure.Time;
using Plastoids.Modules.Payouts.Domain;
using Plastoids.Modules.Payouts.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payouts.Features.Holds.PlaceHold;

/// <summary>Dispute hold: the tutor is skipped by payout runs until released, at most 5 working days (blueprint §18).</summary>
public sealed record PlaceHoldCommand(Guid TutorUserId, string Reason, string IdempotencyKey) : ICommand<HoldView>, IIdempotentCommand;

public sealed record PlaceHoldRequest(Guid TutorUserId, string Reason);

public sealed record ReleaseHoldCommand(Guid HoldId, string Resolution, string IdempotencyKey) : ICommand<HoldView>, IIdempotentCommand;

public sealed record ReleaseHoldRequest(string Resolution);

public sealed record HoldView(Guid HoldId, Guid TutorUserId, string Reason, DateTime PlacedAt, DateTime ReleasesAt, DateTime? ReleasedAt, string? Resolution);

internal sealed class PlaceHoldValidator : AbstractValidator<PlaceHoldCommand>
{
    public PlaceHoldValidator()
    {
        RuleFor(x => x.TutorUserId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class ReleaseHoldValidator : AbstractValidator<ReleaseHoldCommand>
{
    public ReleaseHoldValidator()
    {
        RuleFor(x => x.HoldId).NotEmpty();
        RuleFor(x => x.Resolution).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class PlaceHoldHandler(PayoutsDbContext db, IOutbox outbox, ICurrentUser currentUser, ICurrentTenant tenant, IClock clock) : ICommandHandler<PlaceHoldCommand, HoldView>
{
    public async Task<Result<HoldView>> Handle(PlaceHoldCommand request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var open = await db.Holds.SingleOrDefaultAsync(h => h.TutorUserId == request.TutorUserId && h.ReleasedAt == null && h.ReleasesAt > now, cancellationToken);
        if (open is not null)
        {
            return View(open);
        }

        var hold = DisputeHold.Place(tenant.TenantId, request.TutorUserId, request.Reason, currentUser.UserId ?? Guid.Empty, now, WorkingDays.Add(now, DisputeHold.MaxWorkingDays));
        db.Holds.Add(hold);
        outbox.Enqueue(NotificationRequested.To(request.TutorUserId, "payouts.hold.placed", new Dictionary<string, string> { ["reason"] = hold.Reason, ["releasesAt"] = hold.ReleasesAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture) }));
        return View(hold);
    }

    internal static HoldView View(DisputeHold h) => new(h.Id, h.TutorUserId, h.Reason, h.PlacedAt, h.ReleasesAt, h.ReleasedAt, h.Resolution);
}

internal sealed class ReleaseHoldHandler(PayoutsDbContext db, IOutbox outbox, ICurrentUser currentUser, IClock clock) : ICommandHandler<ReleaseHoldCommand, HoldView>
{
    public async Task<Result<HoldView>> Handle(ReleaseHoldCommand request, CancellationToken cancellationToken)
    {
        var hold = await db.Holds.SingleOrDefaultAsync(h => h.Id == request.HoldId, cancellationToken);
        if (hold is null)
        {
            return Error.NotFound("hold_not_found", "No such hold.");
        }

        var released = hold.Release(currentUser.UserId ?? Guid.Empty, request.Resolution, clock.UtcNow);
        if (released.IsFailure)
        {
            return Result.Failure<HoldView>(released.Error);
        }

        outbox.Enqueue(NotificationRequested.To(hold.TutorUserId, "payouts.hold.released", new Dictionary<string, string> { ["resolution"] = hold.Resolution ?? string.Empty }));
        return PlaceHoldHandler.View(hold);
    }
}

internal static class HoldEndpoints
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapPost("/holds", async (PlaceHoldRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new PlaceHoldCommand(body.TutorUserId, body.Reason, key), ct)).ToCreatedResult(r => $"/api/v1/admin/payouts/holds/{r.HoldId}");
            })
            .WithName("PlaceHold")
            .WithSummary("Staff: place a dispute hold on a tutor's payouts (auto-releases after 5 working days). Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);

        admin.MapPost("/holds/{holdId:guid}/release", async (Guid holdId, ReleaseHoldRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new ReleaseHoldCommand(holdId, body.Resolution, key), ct)).ToHttpResult();
            })
            .WithName("ReleaseHold")
            .WithSummary("Staff: release a dispute hold with the resolution. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
