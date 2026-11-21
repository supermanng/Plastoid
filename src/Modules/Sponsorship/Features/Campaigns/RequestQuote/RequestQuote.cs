using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Identity.Authorization;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Sponsorship.Features.Campaigns.RequestQuote;

public sealed record RequestQuoteCommand(Guid OrganisationId, Guid ProgrammeId) : ICommand<QuoteResponse>;

public sealed record QuoteLineView(string Kind, string Description, int Quantity, Money UnitAmount, Money Amount);

/// <summary>Story F1: tuition, management fee (12 %, min ₦3m), seat fee (₦12,000 × seats) and VAT (7.5 % on fees) as separate lines.</summary>
public sealed record QuoteResponse(Guid ProgrammeId, int Version, string Status, IReadOnlyList<QuoteLineView> Lines, Money Subtotal, Money Vat, Money Total, Money CoPayPerSeat, int DurationMonths);

internal sealed class RequestQuoteValidator : AbstractValidator<RequestQuoteCommand>
{
    public RequestQuoteValidator()
    {
        RuleFor(x => x.OrganisationId).NotEmpty();
        RuleFor(x => x.ProgrammeId).NotEmpty();
    }
}

internal sealed class RequestQuoteHandler(ProgrammeAccess access, IClock clock) : ICommandHandler<RequestQuoteCommand, QuoteResponse>
{
    public async Task<Result<QuoteResponse>> Handle(RequestQuoteCommand request, CancellationToken cancellationToken)
    {
        var programme = await access.ForOrganisationAsync(request.OrganisationId, request.ProgrammeId, cancellationToken);
        if (programme.IsFailure)
        {
            return Result.Failure<QuoteResponse>(programme.Error);
        }

        var quoted = programme.Value.RequestQuote(clock.UtcNow);
        return quoted.IsFailure ? Result.Failure<QuoteResponse>(quoted.Error) : Build(programme.Value, quoted.Value);
    }

    internal static QuoteResponse Build(Programme p, QuoteLines q)
    {
        var lines = new List<QuoteLineView>
        {
            new("tuition", $"Tuition passed to tutors ({p.SponsorSharePercent}% sponsor share)", q.Seats, q.TuitionPerSeatSponsorShare, q.Tuition),
            new("management_fee", "Programme management fee (12% of tuition, minimum NGN 3,000,000)", 1, q.ManagementFee, q.ManagementFee),
            new("seat_fee", "Platform seat fee (LMS, AI tutor, certificate, reporting)", q.Seats, FeeSchedule.SeatFee, q.SeatFee),
            new("vat", "VAT 7.5% on fees", 1, q.Vat, q.Vat),
        };
        return new QuoteResponse(p.Id, p.QuoteVersion, p.Status.ToString().ToLowerInvariant(), lines, q.Tuition + q.ManagementFee + q.SeatFee, q.Vat, q.Total, p.CoPayPerSeat, p.DurationMonths);
    }
}

internal static class RequestQuoteEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{programmeId:guid}/quote", async (Guid organisationId, Guid programmeId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new RequestQuoteCommand(organisationId, programmeId), ct)).ToHttpResult())
            .WithName("RequestQuote")
            .WithSummary("Sponsor: compute the quote (tuition, management fee 12% min NGN 3m, seat fee NGN 12,000, VAT 7.5% on fees).")
            .RequireAuthorization(IdentityPolicies.OrganisationMember);
    }
}
