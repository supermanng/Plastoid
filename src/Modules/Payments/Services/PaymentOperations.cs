using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payments.Domain;
using Plastoids.Modules.Payments.Persistence;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Payments.Services;

/// <summary>Implementation of the in-process contract used by Booking (and later Sponsorship). Enlists the Payments context in the caller's unit of work.</summary>
internal sealed class PaymentOperations(
    PaymentsDbContext db,
    IUnitOfWork unitOfWork,
    IPaymentGateway gateway,
    IFeeRules fees,
    RefundIssuer issuer,
    UnitCheckout units,
    ILedgerService ledger,
    IOptions<PaymentOptions> options,
    ICurrentTenant tenant) : IPaymentOperations
{
    public async Task<Result<IntentHandle>> CreateIntentAsync(IntentRequest request, CancellationToken cancellationToken)
    {
        if (!PaymentPurposes.IsValid(request.Purpose))
        {
            return Error.Validation("purpose_invalid", "Unknown payment purpose.");
        }

        if (request.Amount.Currency != Money.Naira || request.Amount.AmountKobo <= 0)
        {
            return Error.Validation("amount_invalid", "Amount must be a positive naira amount.");
        }

        await EnlistAsync(cancellationToken);
        var fee = fees.Resolve(new SaleContext(request.Purpose, request.LearnerSource));
        var intent = PaymentIntent.Create(tenant.TenantId, request.PayerId, request.Purpose, request.ReferenceId, null, request.Description, request.Amount, request.PayeeUserId, fee.CommissionBps, fee.RuleCode);
        if (request.PayWithUnits)
        {
            db.Intents.Add(intent);
            var paid = await units.PayAsync(intent, cancellationToken);
            if (paid.IsFailure)
            {
                return Result.Failure<IntentHandle>(paid.Error);
            }

            return new IntentHandle(intent.Id, intent.GatewayReference, null, intent.Amount, fee.CommissionBps, intent.Status.ToString().ToLowerInvariant(), intent.UnitsCharged);
        }

        var callback = new Uri($"{options.Value.CallbackBaseUrl.TrimEnd('/')}/{intent.Id:N}");
        var session = await gateway.InitialiseCheckoutAsync(intent.Id, request.Amount, $"{request.PayerId:N}@{options.Value.PayerEmailFallbackDomain}", callback, cancellationToken);
        if (session.IsFailure)
        {
            return Result.Failure<IntentHandle>(session.Error);
        }

        intent.AttachCheckout(gateway.Name, session.Value.GatewayReference, session.Value.CheckoutUrl.ToString());
        db.Intents.Add(intent);
        return new IntentHandle(intent.Id, intent.GatewayReference, session.Value.CheckoutUrl, intent.Amount, fee.CommissionBps);
    }

    public async Task<Result<RefundHandle>> RefundByPolicyAsync(PolicyRefundRequest request, CancellationToken cancellationToken)
    {
        if (request.Destination is not (RefundDestinations.Card or RefundDestinations.Credit))
        {
            return Error.Validation("refund_destination_invalid", "destination must be card or credit.");
        }

        await EnlistAsync(cancellationToken);
        var intent = await db.Intents.SingleOrDefaultAsync(i => i.Id == request.PaymentIntentId, cancellationToken);
        if (intent is null || intent.Status != PaymentStatus.Succeeded)
        {
            return Error.Conflict("payment_not_succeeded", "Only successful payments can be refunded.");
        }

        if (request.Amount.IsZero)
        {
            return new RefundHandle(Guid.Empty, request.Amount, request.Destination);
        }

        if (request.Amount.IsNegative || request.Amount > intent.RefundableAmount)
        {
            return Error.Validation("refund_exceeds_refundable", $"At most {intent.RefundableAmount} can still be refunded.");
        }

        var refund = Refund.ByPolicy(intent.TenantId, intent.Id, request.Amount, request.Reason, request.Destination, request.LearnerId);
        db.Refunds.Add(refund);
        var issued = await issuer.IssueAsync(intent, refund, cancellationToken);
        if (issued.IsFailure)
        {
            return Result.Failure<RefundHandle>(issued.Error);
        }

        return new RefundHandle(refund.Id, refund.Amount, refund.Destination);
    }

    public Task<Money> GetLearnerCreditAsync(Guid learnerId, CancellationToken cancellationToken)
        => ledger.GetBalanceAsync(AccountCodes.LearnerRefundCredit, learnerId, cancellationToken);

    private async Task EnlistAsync(CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }
    }
}
