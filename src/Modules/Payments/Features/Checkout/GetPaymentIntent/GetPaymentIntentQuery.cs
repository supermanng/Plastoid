using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Payments.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Payments.Features.Checkout.GetPaymentIntent;

public sealed record GetPaymentIntentQuery(Guid PaymentIntentId) : IQuery<PaymentIntentView>;

public sealed record PaymentIntentView(Guid PaymentIntentId, string Purpose, Guid ReferenceId, string Description, Money Amount, string Status, string Gateway, string GatewayReference, Uri CheckoutUrl, DateTime CreatedAt, DateTime? SucceededAt, string? FailureReason, Money RefundedAmount);

internal sealed class GetPaymentIntentHandler(PaymentsDbContext db, ICurrentUser currentUser) : IQueryHandler<GetPaymentIntentQuery, PaymentIntentView>
{
    public async Task<Result<PaymentIntentView>> Handle(GetPaymentIntentQuery request, CancellationToken cancellationToken)
    {
        var i = await db.Intents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.PaymentIntentId, cancellationToken);
        if (i is null || (i.PayerId != currentUser.UserId && !currentUser.Roles.Contains("staff")))
        {
            return Error.NotFound("payment_not_found", "No such payment.");
        }

        return new PaymentIntentView(i.Id, i.Purpose, i.ReferenceId, i.Description, i.Amount, i.Status.ToString().ToLowerInvariant(), i.Gateway, i.GatewayReference, new Uri(i.CheckoutUrl), i.CreatedAt, i.SucceededAt, i.FailureReason, i.RefundedAmount);
    }
}

internal static class GetPaymentIntentEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/intents/{paymentIntentId:guid}", async (Guid paymentIntentId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetPaymentIntentQuery(paymentIntentId), ct)).ToHttpResult())
            .WithName("GetPaymentIntent")
            .RequireAuthorization(Policies.Authenticated);
    }
}
