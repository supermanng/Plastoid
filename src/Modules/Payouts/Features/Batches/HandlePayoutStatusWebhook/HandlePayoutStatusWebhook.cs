using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Modules.Payouts.Domain;
using Plastoids.Modules.Payouts.Persistence;
using Plastoids.Modules.Payouts.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payouts.Features.Batches.HandlePayoutStatusWebhook;

/// <summary>Provider status: settled → entry 4; failed / reversed → funds back on the tutor's balance and a notice.</summary>
public sealed record HandlePayoutStatusWebhookCommand(string Provider, string Payload, string? Signature) : ICommand<PayoutWebhookResponse>;

public sealed record PayoutWebhookResponse(string EventType, string Reference, string Outcome);

internal sealed class HandlePayoutStatusWebhookValidator : AbstractValidator<HandlePayoutStatusWebhookCommand>
{
    public HandlePayoutStatusWebhookValidator()
    {
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Payload).NotEmpty().MaximumLength(64 * 1024);
    }
}

internal sealed class HandlePayoutStatusWebhookHandler(PayoutsDbContext db, IPayoutProvider provider, PayoutSettlement settlement, ICurrentTenant tenant, IClock clock) : ICommandHandler<HandlePayoutStatusWebhookCommand, PayoutWebhookResponse>
{
    public async Task<Result<PayoutWebhookResponse>> Handle(HandlePayoutStatusWebhookCommand request, CancellationToken cancellationToken)
    {
        if (!string.Equals(request.Provider, provider.Name, StringComparison.OrdinalIgnoreCase))
        {
            return Error.NotFound("payout_provider_unknown", "No such payout provider is configured.");
        }

        if (!provider.VerifySignature(request.Payload, request.Signature))
        {
            return Error.Unauthorized("webhook_signature_invalid", "The webhook signature does not match.");
        }

        var parsed = provider.ParseWebhook(request.Payload);
        if (parsed.IsFailure)
        {
            return Result.Failure<PayoutWebhookResponse>(parsed.Error);
        }

        var webhook = parsed.Value;
        if (await db.ProviderEvents.AnyAsync(e => e.Provider == provider.Name && e.EventId == webhook.EventId, cancellationToken))
        {
            return new PayoutWebhookResponse(webhook.EventType, webhook.Reference, "duplicate");
        }

        var now = clock.UtcNow;
        var payout = await db.Payouts.SingleOrDefaultAsync(p => p.ProviderReference == webhook.Reference, cancellationToken);
        string outcome;
        if (payout is null)
        {
            outcome = "unknown_reference";
        }
        else if (webhook.EventType == "transfer.success" && webhook.Amount != payout.Amount)
        {
            outcome = "amount_mismatch";
        }
        else
        {
            var applied = await settlement.ApplyAsync(payout, webhook.EventType, webhook.BankReference, webhook.Reason, now, cancellationToken);
            if (applied.IsFailure)
            {
                return Result.Failure<PayoutWebhookResponse>(applied.Error);
            }

            outcome = applied.Value;
            await settlement.CompleteBatchIfDoneAsync(payout.BatchId, now, cancellationToken);
        }

        db.ProviderEvents.Add(ProviderEvent.Record(tenant.TenantId, provider.Name, webhook.EventId, webhook.EventType, webhook.Reference, outcome, now));
        return new PayoutWebhookResponse(webhook.EventType, webhook.Reference, outcome);
    }
}

internal static class HandlePayoutStatusWebhookEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/webhooks/{provider}", async (string provider, HttpRequest http, ISender sender, CancellationToken ct) =>
            {
                using var reader = new StreamReader(http.Body);
                var payload = await reader.ReadToEndAsync(ct);
                var signature = http.Headers["x-payout-signature"].FirstOrDefault() ?? http.Headers["x-paystack-signature"].FirstOrDefault();
                return (await sender.Send(new HandlePayoutStatusWebhookCommand(provider, payload, signature), ct)).ToHttpResult();
            })
            .WithName("HandlePayoutStatusWebhook")
            .WithSummary("Payout provider webhook (HMAC-verified): transfer.success → ledger entry 4; failed/reversed → balance restored.")
            .AllowAnonymous()
            .DisableRateLimiting(); // HMAC-verified; providers retry in bursts and must not be throttled with ordinary traffic
    }
}
