using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Payments.Domain;
using Plastoids.Modules.Payments.Persistence;
using Plastoids.Modules.Payments.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payments.Features.Settlement.ImportSettlements;

/// <summary>Blueprint entry 2: pull the gateway settlement report and post Dr 1020/5010, Cr 1030 for every settled charge. Idempotent per payment.</summary>
public sealed record ImportSettlementsCommand(DateOnly? UpTo) : ICommand<ImportSettlementsResponse>;

public sealed record ImportSettlementsResponse(int Settled, long GrossKobo, long FeesKobo);

internal sealed class ImportSettlementsValidator : AbstractValidator<ImportSettlementsCommand>
{
}

internal sealed class ImportSettlementsHandler(PaymentsDbContext db, IGatewaySettlementReport report, ILedgerService ledger, IClock clock) : ICommandHandler<ImportSettlementsCommand, ImportSettlementsResponse>
{
    public async Task<Result<ImportSettlementsResponse>> Handle(ImportSettlementsCommand request, CancellationToken cancellationToken)
    {
        var upTo = request.UpTo ?? DateOnly.FromDateTime(clock.UtcNow);
        var candidates = await db.Intents.Where(i => i.Status == PaymentStatus.Succeeded && i.SettledAt == null).ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return new ImportSettlementsResponse(0, 0, 0);
        }

        var settled = await report.GetSettlementsAsync(upTo, candidates.Select(c => c.GatewayReference).ToList(), cancellationToken);
        var settledCount = 0;
        long gross = 0;
        long fees = 0;
        foreach (var row in settled)
        {
            var intent = candidates.SingleOrDefault(c => c.GatewayReference == row.GatewayReference);
            if (intent is null)
            {
                continue;
            }

            // The fake report returns zero fees; fall back to the fee the gateway reported at charge time.
            var fee = row.Fee.IsZero ? intent.GatewayFee : row.Fee;
            var entry = await ledger.PostAsync(PaymentPostings.Entry2Settlement(intent, fee, row.SettledAt), cancellationToken);
            if (entry.IsFailure)
            {
                return Result.Failure<ImportSettlementsResponse>(entry.Error);
            }

            var marked = intent.MarkSettled(fee, row.SettledAt);
            if (marked.IsFailure)
            {
                continue;
            }

            db.Transactions.Add(PaymentTransaction.Record(intent.TenantId, intent.Id, TransactionKinds.Settlement, intent.Amount - fee, fee, intent.GatewayReference, row.SettledAt, entry.Value));
            settledCount++;
            gross += intent.Amount.AmountKobo;
            fees += fee.AmountKobo;
        }

        return new ImportSettlementsResponse(settledCount, gross, fees);
    }
}

internal static class ImportSettlementsEndpoint
{
    public static void Map(IEndpointRouteBuilder adminGroup)
    {
        adminGroup.MapPost("/settlements/import", async (DateOnly? upTo, ISender sender, CancellationToken ct) => (await sender.Send(new ImportSettlementsCommand(upTo), ct)).ToHttpResult())
            .WithName("ImportSettlements")
            .WithSummary("Staff: import the gateway settlement report (entry 2). Also runs daily.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
