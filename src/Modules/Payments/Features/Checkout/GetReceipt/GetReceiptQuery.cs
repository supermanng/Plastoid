using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Payments.Domain;
using Plastoids.Modules.Payments.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Payments.Features.Checkout.GetReceipt;

public sealed record GetReceiptQuery(Guid PaymentIntentId) : IQuery<Receipt>;

public sealed record Receipt(string ReceiptNumber, Guid PaymentIntentId, Guid PayerId, string Description, Money Amount, DateTime PaidAt, string Gateway, string GatewayReference, Money RefundedAmount, string Status);

internal sealed class GetReceiptHandler(PaymentsDbContext db, ICurrentUser currentUser) : IQueryHandler<GetReceiptQuery, Receipt>
{
    public async Task<Result<Receipt>> Handle(GetReceiptQuery request, CancellationToken cancellationToken)
    {
        var i = await db.Intents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.PaymentIntentId, cancellationToken);
        if (i is null || (i.PayerId != currentUser.UserId && !currentUser.Roles.Contains("staff")))
        {
            return Error.NotFound("payment_not_found", "No such payment.");
        }

        if (i.Status != PaymentStatus.Succeeded)
        {
            return Error.Conflict("payment_not_succeeded", "Receipts exist only for successful payments.");
        }

        return new Receipt($"RCP-{i.SucceededAt!.Value:yyyyMM}-{i.Id.ToString("N")[^8..].ToUpperInvariant()}", i.Id, i.PayerId, i.Description, i.Amount, i.SucceededAt.Value, i.Gateway, i.GatewayReference, i.RefundedAmount, i.Status.ToString().ToLowerInvariant());
    }
}

internal static class GetReceiptEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/intents/{paymentIntentId:guid}/receipt", async (Guid paymentIntentId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetReceiptQuery(paymentIntentId), ct)).ToHttpResult())
            .WithName("GetReceipt")
            .RequireAuthorization(Policies.Authenticated);
    }
}
