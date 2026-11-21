using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Payments.Persistence;
using Plastoids.Modules.Payments.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Payments.Features.Refunds.ApproveRefund;

/// <summary>Second approver for refunds above ₦100,000 (maker–checker). approved_by must differ from requested_by.</summary>
public sealed record ApproveRefundCommand(Guid RefundId, string IdempotencyKey) : ICommand<ApproveRefundResponse>, IIdempotentCommand;

public sealed record ApproveRefundResponse(Guid RefundId, string Status);

internal sealed class ApproveRefundValidator : AbstractValidator<ApproveRefundCommand>
{
    public ApproveRefundValidator()
    {
        RuleFor(x => x.RefundId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class ApproveRefundHandler(PaymentsDbContext db, RefundIssuer issuer, ICurrentUser currentUser) : ICommandHandler<ApproveRefundCommand, ApproveRefundResponse>
{
    public async Task<Result<ApproveRefundResponse>> Handle(ApproveRefundCommand request, CancellationToken cancellationToken)
    {
        var refund = await db.Refunds.SingleOrDefaultAsync(r => r.Id == request.RefundId, cancellationToken);
        if (refund is null)
        {
            return Error.NotFound("refund_not_found", "No such refund.");
        }

        var approved = refund.Approve(currentUser.UserId ?? Guid.Empty);
        if (approved.IsFailure)
        {
            return Result.Failure<ApproveRefundResponse>(approved.Error);
        }

        var intent = await db.Intents.SingleAsync(i => i.Id == refund.PaymentIntentId, cancellationToken);
        var issued = await issuer.IssueAsync(intent, refund, cancellationToken);
        if (issued.IsFailure)
        {
            return Result.Failure<ApproveRefundResponse>(issued.Error);
        }

        return new ApproveRefundResponse(refund.Id, refund.Status.ToString().ToLowerInvariant());
    }
}

internal static class ApproveRefundEndpoint
{
    public static void Map(IEndpointRouteBuilder adminGroup)
    {
        adminGroup.MapPost("/refunds/{refundId:guid}/approve", async (Guid refundId, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new ApproveRefundCommand(refundId, key), ct)).ToHttpResult();
            })
            .WithName("ApproveRefund")
            .WithSummary("Staff (checker): approve and issue a refund above ₦100,000. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
