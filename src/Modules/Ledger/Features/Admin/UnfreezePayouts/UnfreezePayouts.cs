using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Ledger.Domain;
using Plastoids.Modules.Ledger.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Ledger.Features.Admin.UnfreezePayouts;

/// <summary>Finance lifts the payouts freeze once the shortfall is explained or covered. A human decision, never automatic (blueprint §18 safeguards).</summary>
public sealed record UnfreezePayoutsCommand(string Reason, string IdempotencyKey) : ICommand<UnfreezePayoutsResponse>, IIdempotentCommand;

public sealed record UnfreezePayoutsRequest(string Reason);

public sealed record UnfreezePayoutsResponse(bool PayoutsFrozen, DateTime? LastReconciledAt);

internal sealed class UnfreezePayoutsValidator : AbstractValidator<UnfreezePayoutsCommand>
{
    public UnfreezePayoutsValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(10).MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class UnfreezePayoutsHandler(LedgerDbContext db) : ICommandHandler<UnfreezePayoutsCommand, UnfreezePayoutsResponse>
{
    public async Task<Result<UnfreezePayoutsResponse>> Handle(UnfreezePayoutsCommand request, CancellationToken cancellationToken)
    {
        var control = await db.Controls.SingleOrDefaultAsync(c => c.Id == LedgerControl.SingletonId, cancellationToken);
        if (control is null)
        {
            return Error.NotFound("ledger_control_missing", "No reconciliation has run yet.");
        }

        control.Unfreeze();
        return new UnfreezePayoutsResponse(control.PayoutsFrozen, control.LastReconciledAt);
    }
}

internal static class UnfreezePayoutsEndpoint
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapPost("/reconciliation/unfreeze", async (UnfreezePayoutsRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new UnfreezePayoutsCommand(body.Reason, key), ct)).ToHttpResult();
            })
            .WithName("UnfreezePayouts")
            .WithSummary("Staff finance: lift the payouts freeze with a reason (audited). Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
