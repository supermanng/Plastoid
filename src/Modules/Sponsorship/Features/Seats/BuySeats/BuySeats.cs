using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Identity.Authorization;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Ledger.Contracts;
using Plastoids.Modules.Lms.Contracts;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.Modules.Wallet.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Sponsorship.Features.Seats.BuySeats;

/// <summary>
/// ADR 0021 §2: a verified organisation buys N seats in a cohort. Whole units cover Seats × price at today's rate, the wallet
/// releases them first-in, first-out, entry 14 moves the naira into programme funds held under the block, the cohort reserves the seats.
/// </summary>
public sealed record BuySeatsCommand(Guid OrganisationId, Guid CohortId, int Seats, string IdempotencyKey) : ICommand<SeatBlockView>, IIdempotentCommand;

public sealed record BuySeatsRequest(Guid CohortId, int Seats);

internal sealed class BuySeatsValidator : AbstractValidator<BuySeatsCommand>
{
    public BuySeatsValidator()
    {
        RuleFor(x => x.OrganisationId).NotEmpty();
        RuleFor(x => x.CohortId).NotEmpty();
        RuleFor(x => x.Seats).InclusiveBetween(1, SeatBlock.MaxSeats).WithErrorCode("seats_invalid");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class BuySeatsHandler(
    SponsorshipDbContext db,
    IOrganisationDirectory organisations,
    ICohortSeats cohorts,
    ICourseOutlineReader courses,
    IUnitRates rates,
    IUnitWallet wallets,
    ILedgerService ledger,
    ICurrentUser currentUser,
    ICurrentTenant tenant,
    IClock clock) : ICommandHandler<BuySeatsCommand, SeatBlockView>
{
    public async Task<Result<SeatBlockView>> Handle(BuySeatsCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var organisation = await organisations.GetAsync(request.OrganisationId, cancellationToken);
        if (organisation is null)
        {
            return Error.NotFound("organisation_not_found", "No such organisation.");
        }

        if (!organisation.IsVerified)
        {
            return Error.Conflict("organisation_not_verified", "The organisation must be verified before it sponsors seats.");
        }

        var now = clock.UtcNow;
        var cohort = await cohorts.GetAsync(request.CohortId, cancellationToken);
        if (cohort is null)
        {
            return Error.NotFound("cohort_not_found", "No such cohort.");
        }

        if (cohort.HasEnded(now))
        {
            return Error.Conflict("cohort_ended", "The cohort has already ended.");
        }

        var course = await courses.GetPublishedAsync(cohort.CourseId, null, cancellationToken);
        var offering = course?.Offerings.SingleOrDefault(o => o.OfferingId == cohort.OfferingId);
        if (course is null || offering is null)
        {
            return Error.Conflict("course_not_published", "The cohort's course is not published.");
        }

        var created = SeatBlock.Purchase(tenant.TenantId, organisation.OrganisationId, organisation.Name,
            new SeatBlockCohort(cohort.CohortId, cohort.Name, course.CourseId, course.Title, course.Slug, offering.OfferingId, cohort.TutorUserId, cohort.StartsAt, cohort.EndsAt),
            request.Seats, offering.Price, userId, now);
        if (created.IsFailure)
        {
            return Result.Failure<SeatBlockView>(created.Error);
        }

        var block = created.Value;
        var reserved = await cohorts.ReserveAsync(cohort.CohortId, request.Seats, cancellationToken);
        if (reserved.IsFailure)
        {
            return Result.Failure<SeatBlockView>(reserved.Error);
        }

        var rate = await rates.CurrentAsync(cancellationToken);
        var units = rate.UnitsFor(block.TotalPrice);
        var redeemed = await wallets.RedeemAsync(new UnitRedemptionRequest(WalletOwnerTypes.Organisation, block.OrganisationId, units, SponsorshipPostings.PurchaseReference(block.Id), "seat_block", block.Id, $"{block.Seats} seats in {block.CohortName}"), cancellationToken);
        if (redeemed.IsFailure)
        {
            return Result.Failure<SeatBlockView>(redeemed.Error);
        }

        block.Fund(units, rate.RateKobo, redeemed.Value.Value, redeemed.Value.WalletId);
        var posted = await ledger.PostAsync(SponsorshipPostings.Entry14SeatBlockPurchase(block, redeemed.Value.Value, redeemed.Value.WalletId, now), cancellationToken);
        if (posted.IsFailure)
        {
            return Result.Failure<SeatBlockView>(posted.Error);
        }

        block.LinkLedgerEntry(posted.Value);
        db.SeatBlocks.Add(block);
        return SeatViews.Block(block, 0, 0);
    }
}

internal static class BuySeatsEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/", async (Guid organisationId, BuySeatsRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new BuySeatsCommand(organisationId, body.CohortId, body.Seats, key), ct)).ToCreatedResult(v => $"/api/v1/sponsorship/organisations/{organisationId}/seat-blocks/{v.BlockId}");
            })
            .WithName("BuySeatBlock")
            .WithSummary("Sponsor owner/finance: buy seats in a cohort with units from the organisation wallet (entry 14). Requires Idempotency-Key.")
            .RequireAuthorization(IdentityPolicies.OrganisationFinance);
    }
}
