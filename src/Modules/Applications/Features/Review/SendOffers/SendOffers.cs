using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Applications.Contracts;
using Plastoids.Modules.Applications.Domain;
using Plastoids.Modules.Applications.Persistence;
using Plastoids.Modules.Applications.Services;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Applications.Features.Review.SendOffers;

/// <summary>Offers to accepted applicants in score order once the shortlist is approved; accepted applicants beyond the seats go to the waitlist.</summary>
public sealed record SendOffersCommand(Guid ProgrammeId, string IdempotencyKey) : ICommand<SendOffersResponse>, IIdempotentCommand;

public sealed record SendOffersResponse(Guid ProgrammeId, int Sent, int Waitlisted, int SeatsOpen);

internal sealed class SendOffersValidator : AbstractValidator<SendOffersCommand>
{
    public SendOffersValidator()
    {
        RuleFor(x => x.ProgrammeId).NotEmpty();
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class SendOffersHandler(ApplicationsDbContext db, IProgrammeReader programmes, IProgrammeSeats seats, OfferService offers, IOutbox outbox, ICurrentUser currentUser, IClock clock) : ICommandHandler<SendOffersCommand, SendOffersResponse>
{
    public async Task<Result<SendOffersResponse>> Handle(SendOffersCommand request, CancellationToken cancellationToken)
    {
        var programme = await programmes.GetAsync(request.ProgrammeId, cancellationToken);
        if (programme is null)
        {
            return Error.NotFound("programme_not_found", "No such programme.");
        }

        if (!await db.ShortlistApprovals.AnyAsync(s => s.ProgrammeId == programme.ProgrammeId, cancellationToken))
        {
            return Error.Conflict("shortlist_not_approved", "The sponsor must approve the shortlist before offers go out.");
        }

        var accepted = await db.Applications.Where(a => a.ProgrammeId == programme.ProgrammeId && a.Status == ApplicationStatus.Accepted)
            .OrderByDescending(a => a.TotalScore).ThenBy(a => a.SubmittedAt).ToListAsync(cancellationToken);
        var now = clock.UtcNow;
        var open = await seats.CountOpenAsync(programme.ProgrammeId, cancellationToken);
        var sent = 0;
        var waitlisted = 0;
        var reviewer = currentUser.UserId ?? Guid.Empty;
        foreach (var application in accepted)
        {
            if (sent < open)
            {
                var offer = await offers.SendOfferAsync(application, programme, now, cancellationToken);
                if (offer.IsSuccess)
                {
                    sent++;
                    continue;
                }
            }

            application.Decide(DecisionKinds.Waitlisted, now);
            db.Decisions.Add(Decision.Record(application.TenantId, application, DecisionKinds.Waitlisted, "No seat available at offer time; waitlisted in score order", reviewer, "system", now));
            outbox.Enqueue(new DecisionRecorded(application.Id, application.Id, application.ProgrammeId, application.LearnerId, DecisionKinds.Waitlisted, reviewer));
            waitlisted++;
        }

        return new SendOffersResponse(programme.ProgrammeId, sent, waitlisted, open - sent);
    }
}

internal static class SendOffersEndpoint
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapPost("/programmes/{programmeId:guid}/offers", async (Guid programmeId, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new SendOffersCommand(programmeId, key), ct)).ToHttpResult();
            })
            .WithName("SendOffers")
            .WithSummary("Staff: send offers (seat reserved, 5-day expiry) to accepted applicants in score order; the rest are waitlisted. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
