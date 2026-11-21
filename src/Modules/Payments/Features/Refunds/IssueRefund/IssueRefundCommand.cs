using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Payments.Domain;
using Plastoids.Modules.Payments.Persistence;
using Plastoids.Modules.Payments.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Payments.Features.Refunds.IssueRefund;

/// <summary>Staff refund (entry 5). Up to ₦100,000 issues immediately; above that it waits for a second approver.</summary>
public sealed record IssueRefundCommand(Guid PaymentIntentId, Money Amount, string Reason, string IdempotencyKey) : ICommand<IssueRefundResponse>, IIdempotentCommand;

public sealed record IssueRefundRequest(Money Amount, string Reason);

public sealed record IssueRefundResponse(Guid RefundId, string Status, bool NeedsSecondApprover, Money Amount);

internal sealed class IssueRefundValidator : AbstractValidator<IssueRefundCommand>
{
    public IssueRefundValidator()
    {
        RuleFor(x => x.PaymentIntentId).NotEmpty();
        RuleFor(x => x.Amount.AmountKobo).GreaterThan(0);
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(5).MaximumLength(500);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class IssueRefundHandler(PaymentsDbContext db, RefundIssuer issuer, ICurrentUser currentUser) : ICommandHandler<IssueRefundCommand, IssueRefundResponse>
{
    public async Task<Result<IssueRefundResponse>> Handle(IssueRefundCommand request, CancellationToken cancellationToken)
    {
        var intent = await db.Intents.SingleOrDefaultAsync(i => i.Id == request.PaymentIntentId, cancellationToken);
        if (intent is null)
        {
            return Error.NotFound("payment_not_found", "No such payment.");
        }

        if (intent.Status != PaymentStatus.Succeeded)
        {
            return Error.Conflict("payment_not_succeeded", "Only successful payments can be refunded.");
        }

        if (request.Amount.Currency != intent.Amount.Currency || request.Amount > intent.RefundableAmount)
        {
            return Error.Validation("refund_exceeds_refundable", $"At most {intent.RefundableAmount} can still be refunded.");
        }

        var staffId = currentUser.UserId ?? Guid.Empty;
        var refund = Refund.Request(intent.TenantId, intent.Id, request.Amount, request.Reason, staffId);
        db.Refunds.Add(refund);
        if (!refund.NeedsSecondApprover)
        {
            var approved = refund.Approve(staffId);
            if (approved.IsFailure)
            {
                return Result.Failure<IssueRefundResponse>(approved.Error);
            }

            var issued = await issuer.IssueAsync(intent, refund, cancellationToken);
            if (issued.IsFailure)
            {
                return Result.Failure<IssueRefundResponse>(issued.Error);
            }
        }

        return new IssueRefundResponse(refund.Id, refund.Status.ToString().ToLowerInvariant(), refund.NeedsSecondApprover, refund.Amount);
    }
}

internal static class IssueRefundEndpoint
{
    public static void Map(IEndpointRouteBuilder adminGroup)
    {
        adminGroup.MapPost("/intents/{paymentIntentId:guid}/refunds", async (Guid paymentIntentId, IssueRefundRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new IssueRefundCommand(paymentIntentId, body.Amount, body.Reason, key), ct)).ToCreatedResult(r => $"/api/v1/admin/payments/refunds/{r.RefundId}");
            })
            .WithName("IssueRefund")
            .WithSummary("Staff: refund (entry 5). Above ₦100,000 a different staff member must approve. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
