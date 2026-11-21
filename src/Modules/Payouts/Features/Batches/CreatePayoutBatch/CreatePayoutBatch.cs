using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Payouts.Domain;
using Plastoids.Modules.Payouts.Persistence;
using Plastoids.Modules.Payouts.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payouts.Features.Batches.CreatePayoutBatch;

/// <summary>Maker step: draft the batch from payable balances (≥ ₦5,000, name-matched account, no open dispute, payouts not frozen).</summary>
public sealed record CreatePayoutBatchCommand(string IdempotencyKey) : ICommand<BatchView>, IIdempotentCommand;

public sealed record BatchPayoutView(Guid PayoutId, Guid TutorUserId, Money Amount, string Status, string AccountNumberMasked, string AccountName, string? ProviderReference, string? BankReference, string? FailureReason);

public sealed record BatchView(Guid BatchId, string Reference, string Status, Guid CreatedBy, Guid? ApprovedBy, int Count, Money Total, string? Provider, DateTime CreatedAt, IReadOnlyList<BatchPayoutView> Payouts, IReadOnlyList<SkippedTutor> Skipped)
{
    public static BatchView From(PayoutBatch b, IEnumerable<Payout> payouts, IReadOnlyList<SkippedTutor>? skipped = null)
        => new(b.Id, b.Reference, b.Status.ToString().ToLowerInvariant(), b.CreatedBy, b.ApprovedBy, b.Count, b.Total, b.Provider, b.CreatedAt,
            payouts.Select(p => new BatchPayoutView(p.Id, p.TutorUserId, p.Amount, p.Status.ToString().ToLowerInvariant(), p.AccountNumberMasked, p.AccountName, p.ProviderReference, p.BankReference, p.FailureReason)).ToList(), skipped ?? []);
}

public sealed record GetPayoutBatchQuery(Guid BatchId) : IQuery<BatchView>;

internal sealed class CreatePayoutBatchValidator : AbstractValidator<CreatePayoutBatchCommand>
{
    public CreatePayoutBatchValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class CreatePayoutBatchHandler(PayoutRunner runner, ICurrentUser currentUser, IClock clock) : ICommandHandler<CreatePayoutBatchCommand, BatchView>
{
    public async Task<Result<BatchView>> Handle(CreatePayoutBatchCommand request, CancellationToken cancellationToken)
    {
        var draft = await runner.CreateBatchAsync(currentUser.UserId ?? Guid.Empty, clock.UtcNow, cancellationToken);
        return draft.IsFailure ? Result.Failure<BatchView>(draft.Error) : BatchView.From(draft.Value.Batch, draft.Value.Payouts, draft.Value.Skipped);
    }
}

internal sealed class GetPayoutBatchHandler(PayoutsDbContext db) : IQueryHandler<GetPayoutBatchQuery, BatchView>
{
    public async Task<Result<BatchView>> Handle(GetPayoutBatchQuery request, CancellationToken cancellationToken)
    {
        var batch = await db.Batches.AsNoTracking().SingleOrDefaultAsync(b => b.Id == request.BatchId, cancellationToken);
        if (batch is null)
        {
            return Error.NotFound("batch_not_found", "No such payout batch.");
        }

        var payouts = await db.Payouts.AsNoTracking().Where(p => p.BatchId == batch.Id).OrderBy(p => p.CreatedAt).ToListAsync(cancellationToken);
        return BatchView.From(batch, payouts);
    }
}

internal static class CreatePayoutBatchEndpoint
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapPost("/batches", async (HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new CreatePayoutBatchCommand(key), ct)).ToCreatedResult(r => $"/api/v1/admin/payouts/batches/{r.BatchId}");
            })
            .WithName("CreatePayoutBatch")
            .WithSummary("Staff (maker): draft this week's payout batch. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);

        admin.MapGet("/batches/{batchId:guid}", async (Guid batchId, ISender sender, CancellationToken ct) => (await sender.Send(new GetPayoutBatchQuery(batchId), ct)).ToHttpResult())
            .WithName("GetPayoutBatch")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
