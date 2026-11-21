using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Modules.Payments.Domain;
using Plastoids.Modules.Payments.Persistence;
using Plastoids.Modules.Payments.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payments.Features.Checkout.HandleGatewayWebhook;

internal sealed class HandleGatewayWebhookHandler(PaymentsDbContext db, IPaymentGateway gateway, PaymentProcessing processing, ICurrentTenant tenant, IClock clock)
    : ICommandHandler<HandleGatewayWebhookCommand, HandleGatewayWebhookResponse>
{
    public async Task<Result<HandleGatewayWebhookResponse>> Handle(HandleGatewayWebhookCommand request, CancellationToken cancellationToken)
    {
        var parsed = gateway.ParseWebhook(request.Payload);
        if (parsed.IsFailure)
        {
            return Result.Failure<HandleGatewayWebhookResponse>(parsed.Error);
        }

        var webhook = parsed.Value;
        if (await db.GatewayEvents.AnyAsync(e => e.Gateway == request.Gateway && e.EventId == webhook.EventId, cancellationToken))
        {
            return new HandleGatewayWebhookResponse(webhook.EventType, webhook.Reference, "duplicate");
        }

        string outcome;
        switch (webhook.EventType)
        {
            case "charge.success":
            case "charge.failed":
                var intent = await db.Intents.SingleOrDefaultAsync(i => i.GatewayReference == webhook.Reference, cancellationToken);
                if (intent is null)
                {
                    outcome = "unknown_reference";
                    break;
                }

                if (webhook.EventType == "charge.success")
                {
                    if (webhook.Amount != intent.Amount)
                    {
                        outcome = "amount_mismatch";
                        break;
                    }

                    var confirmed = await processing.ConfirmAsync(intent, webhook.Fee, webhook.OccurredAt == DateTime.UnixEpoch ? clock.UtcNow : webhook.OccurredAt, cancellationToken);
                    if (confirmed.IsFailure)
                    {
                        return Result.Failure<HandleGatewayWebhookResponse>(confirmed.Error);
                    }

                    outcome = confirmed.Value ? "confirmed" : "already_confirmed";
                }
                else
                {
                    processing.Fail(intent, "Gateway reported the charge failed.");
                    outcome = "failed";
                }

                break;
            case "virtual_account.credit":
                var matched = await processing.MatchTransferAsync(webhook, cancellationToken);
                if (matched.IsFailure)
                {
                    return Result.Failure<HandleGatewayWebhookResponse>(matched.Error);
                }

                outcome = matched.Value;
                break;
            default:
                outcome = "ignored";
                break;
        }

        db.GatewayEvents.Add(GatewayEvent.Record(tenant.TenantId, request.Gateway, webhook.EventId, webhook.EventType, webhook.Reference, clock.UtcNow, outcome));
        return new HandleGatewayWebhookResponse(webhook.EventType, webhook.Reference, outcome);
    }
}
