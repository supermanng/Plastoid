using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Booking.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Booking.Features.Sessions.QuoteBooking;

/// <summary>Price and availability of a slot before checkout.</summary>
public sealed record QuoteBookingQuery(Guid TutorId, DateTime SlotStart, int DurationMinutes, string Format) : IQuery<SlotQuote>;

internal sealed class QuoteBookingHandler(BookingPricing pricing) : IQueryHandler<QuoteBookingQuery, SlotQuote>
{
    public Task<Result<SlotQuote>> Handle(QuoteBookingQuery request, CancellationToken cancellationToken)
        => pricing.QuoteAsync(request.TutorId, request.Format, request.SlotStart, request.DurationMinutes, cancellationToken);
}

internal static class QuoteBookingEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/quote", async (Guid tutorId, DateTime slotStart, int? durationMinutes, string? format, ISender sender, CancellationToken ct) =>
                (await sender.Send(new QuoteBookingQuery(tutorId, slotStart, durationMinutes ?? 60, format ?? "one_to_one"), ct)).ToHttpResult())
            .WithName("QuoteBooking")
            .WithSummary("Price and availability of a slot: tutorId, slotStart (UTC), durationMinutes, format.")
            .AllowAnonymous();
    }
}
