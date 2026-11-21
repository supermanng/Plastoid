using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Modules.Payments.Domain;
using Plastoids.Modules.Payments.Persistence;
using Plastoids.Modules.Payments.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payments.Features.Checkout.VerifyPaymentIntent;

internal sealed class VerifyPaymentIntentHandler(PaymentsDbContext db, IPaymentGateway gateway, PaymentProcessing processing, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<VerifyPaymentIntentCommand, VerifyPaymentIntentResponse>
{
    public async Task<Result<VerifyPaymentIntentResponse>> Handle(VerifyPaymentIntentCommand request, CancellationToken cancellationToken)
    {
        var intent = await db.Intents.SingleOrDefaultAsync(i => i.Id == request.PaymentIntentId && i.PayerId == currentUser.UserId, cancellationToken);
        if (intent is null)
        {
            return Error.NotFound("payment_not_found", "No such payment.");
        }

        if (intent.Status != PaymentStatus.Pending)
        {
            return new VerifyPaymentIntentResponse(intent.Id, intent.Status.ToString().ToLowerInvariant(), "final");
        }

        var status = await gateway.VerifyAsync(intent.GatewayReference, cancellationToken);
        if (status.IsFailure)
        {
            return Result.Failure<VerifyPaymentIntentResponse>(status.Error);
        }

        switch (status.Value.Status)
        {
            case "success" when status.Value.Amount == intent.Amount:
                var confirmed = await processing.ConfirmAsync(intent, status.Value.Fee, status.Value.PaidAt ?? clock.UtcNow, cancellationToken);
                if (confirmed.IsFailure)
                {
                    return Result.Failure<VerifyPaymentIntentResponse>(confirmed.Error);
                }

                break;
            case "failed":
            case "abandoned":
                processing.Fail(intent, $"Gateway status: {status.Value.Status}");
                break;
        }

        return new VerifyPaymentIntentResponse(intent.Id, intent.Status.ToString().ToLowerInvariant(), status.Value.Status);
    }
}
