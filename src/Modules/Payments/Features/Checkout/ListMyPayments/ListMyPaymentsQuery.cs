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

namespace Plastoids.Modules.Payments.Features.Checkout.ListMyPayments;

public sealed record ListMyPaymentsQuery(string? Cursor, int? Limit) : IQuery<Page<PaymentSummary>>;

public sealed record PaymentSummary(Guid PaymentIntentId, string Purpose, string Description, Money Amount, string Status, DateTime CreatedAt, DateTime? SucceededAt, Money RefundedAmount);

internal sealed class ListMyPaymentsHandler(PaymentsDbContext db, ICurrentUser currentUser) : IQueryHandler<ListMyPaymentsQuery, Page<PaymentSummary>>
{
    public async Task<Result<Page<PaymentSummary>>> Handle(ListMyPaymentsQuery request, CancellationToken cancellationToken)
    {
        var limit = Cursor.ClampLimit(request.Limit);
        var query = db.Intents.AsNoTracking().Where(i => i.PayerId == currentUser.UserId);
        if (Cursor.Decode(request.Cursor) is { } after)
        {
            query = query.Where(i => i.Id.CompareTo(after) < 0);
        }

        var rows = await query.OrderByDescending(i => i.Id).Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;
        var items = rows.Take(limit).Select(i => new PaymentSummary(i.Id, i.Purpose, i.Description, i.Amount, i.Status.ToString().ToLowerInvariant(), i.CreatedAt, i.SucceededAt, i.RefundedAmount)).ToList();
        return new Page<PaymentSummary>(items, hasMore ? Cursor.Encode(items[^1].PaymentIntentId) : null);
    }
}

internal static class ListMyPaymentsEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/me", async (string? cursor, int? limit, ISender sender, CancellationToken ct) => (await sender.Send(new ListMyPaymentsQuery(cursor, limit), ct)).ToHttpResult())
            .WithName("ListMyPayments")
            .RequireAuthorization(Policies.Authenticated);
    }
}
