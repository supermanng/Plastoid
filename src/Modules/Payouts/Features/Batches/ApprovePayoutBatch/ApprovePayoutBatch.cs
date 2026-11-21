using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Payouts.Contracts;
using Plastoids.Modules.Payouts.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payouts.Features.Batches.ApprovePayoutBatch;

/// <summary>Checker step. The domain refuses the maker as checker.</summary>
public sealed record ApprovePayoutBatchCommand(Guid BatchId, string IdempotencyKey) : ICommand<ApprovePayoutBatchResponse>, IIdempotentCommand;

public sealed record RejectPayoutBatchCommand(Guid BatchId, string Reason, string IdempotencyKey) : ICommand<ApprovePayoutBatchResponse>, IIdempotentCommand;

public sealed record RejectPayoutBatchRequest(string Reason);

public sealed record ApprovePayoutBatchResponse(Guid BatchId, string Status, Guid CreatedBy, Guid? ApprovedBy, int Count, Money Total);

internal sealed class ApprovePayoutBatchValidator : AbstractValidator<ApprovePayoutBatchCommand>
{
    public ApprovePayoutBatchValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class RejectPayoutBatchValidator : AbstractValidator<RejectPayoutBatchCommand>
{
    public RejectPayoutBatchValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class ApprovePayoutBatchHandler(PayoutsDbContext db, IOutbox outbox, ICurrentUser currentUser, IClock clock) : ICommandHandler<ApprovePayoutBatchCommand, ApprovePayoutBatchResponse>
{
    public async Task<Result<ApprovePayoutBatchResponse>> Handle(ApprovePayoutBatchCommand request, CancellationToken cancellationToken)
    {
        var batch = await db.Batches.SingleOrDefaultAsync(b => b.Id == request.BatchId, cancellationToken);
        if (batch is null)
        {
            return Error.NotFound("batch_not_found", "No such payout batch.");
        }

        var approved = batch.Approve(currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        if (approved.IsFailure)
        {
            return Result.Failure<ApprovePayoutBatchResponse>(approved.Error);
        }

        outbox.Enqueue(new BatchApproved(batch.Id, batch.Id, batch.Count, batch.Total));
        return new ApprovePayoutBatchResponse(batch.Id, batch.Status.ToString().ToLowerInvariant(), batch.CreatedBy, batch.ApprovedBy, batch.Count, batch.Total);
    }
}

internal sealed class RejectPayoutBatchHandler(PayoutsDbContext db, ICurrentUser currentUser) : ICommandHandler<RejectPayoutBatchCommand, ApprovePayoutBatchResponse>
{
    public async Task<Result<ApprovePayoutBatchResponse>> Handle(RejectPayoutBatchCommand request, CancellationToken cancellationToken)
    {
        var batch = await db.Batches.SingleOrDefaultAsync(b => b.Id == request.BatchId, cancellationToken);
        if (batch is null)
        {
            return Error.NotFound("batch_not_found", "No such payout batch.");
        }

        var rejected = batch.Reject(currentUser.UserId ?? Guid.Empty, request.Reason);
        if (rejected.IsFailure)
        {
            return Result.Failure<ApprovePayoutBatchResponse>(rejected.Error);
        }

        foreach (var payout in await db.Payouts.Where(p => p.BatchId == batch.Id).ToListAsync(cancellationToken))
        {
            payout.Cancel();
        }

        return new ApprovePayoutBatchResponse(batch.Id, batch.Status.ToString().ToLowerInvariant(), batch.CreatedBy, batch.ApprovedBy, batch.Count, batch.Total);
    }
}

internal static class ApprovePayoutBatchEndpoints
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapPost("/batches/{batchId:guid}/approve", async (Guid batchId, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new ApprovePayoutBatchCommand(batchId, key), ct)).ToHttpResult();
            })
            .WithName("ApprovePayoutBatch")
            .WithSummary("Staff (checker ≠ maker): approve the batch. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);

        admin.MapPost("/batches/{batchId:guid}/reject", async (Guid batchId, RejectPayoutBatchRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new RejectPayoutBatchCommand(batchId, body.Reason, key), ct)).ToHttpResult();
            })
            .WithName("RejectPayoutBatch")
            .WithSummary("Staff: reject a pending batch; its payouts are cancelled and the balances stay payable. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
