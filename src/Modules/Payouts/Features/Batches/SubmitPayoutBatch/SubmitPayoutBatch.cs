using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Payouts.Domain;
using Plastoids.Modules.Payouts.Persistence;
using Plastoids.Modules.Payouts.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payouts.Features.Batches.SubmitPayoutBatch;

/// <summary>Sends the approved batch to the payout provider (gateway transfers, bank bulk file as fallback).</summary>
public sealed record SubmitPayoutBatchCommand(Guid BatchId, string IdempotencyKey) : ICommand<SubmitPayoutBatchResponse>, IIdempotentCommand;

public sealed record SubmitPayoutBatchResponse(Guid BatchId, string Status, string Provider, int Submitted, int FailedAtSubmission);

internal sealed class SubmitPayoutBatchValidator : AbstractValidator<SubmitPayoutBatchCommand>
{
    public SubmitPayoutBatchValidator()
    {
        RuleFor(x => x.BatchId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class SubmitPayoutBatchHandler(PayoutsDbContext db, IPayoutProvider provider, PayeeDestinations destinations, PayoutSettlement settlement, IClock clock) : ICommandHandler<SubmitPayoutBatchCommand, SubmitPayoutBatchResponse>
{
    public async Task<Result<SubmitPayoutBatchResponse>> Handle(SubmitPayoutBatchCommand request, CancellationToken cancellationToken)
    {
        var batch = await db.Batches.SingleOrDefaultAsync(b => b.Id == request.BatchId, cancellationToken);
        if (batch is null)
        {
            return Error.NotFound("batch_not_found", "No such payout batch.");
        }

        var now = clock.UtcNow;
        var marked = batch.MarkSubmitted(provider.Name, now);
        if (marked.IsFailure)
        {
            return Result.Failure<SubmitPayoutBatchResponse>(marked.Error);
        }

        var payouts = await db.Payouts.Where(p => p.BatchId == batch.Id && p.Status == PayoutStatus.Pending).ToListAsync(cancellationToken);
        var instructions = new List<TransferInstruction>();
        foreach (var payout in payouts)
        {
            // The full account number is read at submission time only; it is never stored on the payout.
            var account = await destinations.GetAsync(payout.PayeeType, payout.PayeeId, cancellationToken);
            if (account is null)
            {
                payout.Fail("Payout account removed before submission", now);
                continue;
            }

            instructions.Add(new TransferInstruction(payout.Id, account.BankCode, account.AccountNumber, account.AccountName, payout.Amount, $"Plastoids payout {batch.Reference}"));
        }

        var receipts = await provider.SubmitAsync(instructions, cancellationToken);
        if (receipts.IsFailure)
        {
            return Result.Failure<SubmitPayoutBatchResponse>(receipts.Error);
        }

        var submitted = 0;
        var failed = payouts.Count(p => p.Status == PayoutStatus.Failed);
        foreach (var receipt in receipts.Value)
        {
            var payout = payouts.Single(p => p.Id == receipt.PayoutId);
            payout.MarkSubmitted(receipt.ProviderReference, now);
            if (receipt.Status == "failed")
            {
                await settlement.ApplyAsync(payout, "transfer.failed", null, "Rejected by the provider at submission", now, cancellationToken);
                failed++;
            }
            else if (receipt.Status == "settled")
            {
                await settlement.ApplyAsync(payout, "transfer.success", receipt.ProviderReference, null, now, cancellationToken);
                submitted++;
            }
            else
            {
                submitted++;
            }
        }

        batch.TryComplete(payouts, now);
        return new SubmitPayoutBatchResponse(batch.Id, batch.Status.ToString().ToLowerInvariant(), provider.Name, submitted, failed);
    }
}

internal static class SubmitPayoutBatchEndpoint
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapPost("/batches/{batchId:guid}/submit", async (Guid batchId, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new SubmitPayoutBatchCommand(batchId, key), ct)).ToHttpResult();
            })
            .WithName("SubmitPayoutBatch")
            .WithSummary("Staff: submit an approved batch to the payout provider. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
