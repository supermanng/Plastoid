using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Identity.Authorization;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Sponsorship.Features.Campaigns.PublishCampaign;

/// <summary>Publish the opportunity: only after payments.invoice-paid.v1 funded it (or a payment plan was approved) and programme review passed. Creates the seat inventory.</summary>
public sealed record PublishCampaignCommand(Guid? OrganisationId, Guid ProgrammeId) : ICommand<PublishCampaignResponse>;

public sealed record PublishCampaignResponse(Guid ProgrammeId, string Status, int SeatsUnlocked, DateTime PublishedAt);

internal sealed class PublishCampaignValidator : AbstractValidator<PublishCampaignCommand>
{
    public PublishCampaignValidator()
    {
        RuleFor(x => x.ProgrammeId).NotEmpty();
    }
}

internal sealed class PublishCampaignHandler(SponsorshipDbContext db, ProgrammeAccess access, IOutbox outbox, ICurrentUser currentUser, IClock clock) : ICommandHandler<PublishCampaignCommand, PublishCampaignResponse>
{
    public async Task<Result<PublishCampaignResponse>> Handle(PublishCampaignCommand request, CancellationToken cancellationToken)
    {
        var loaded = request.OrganisationId is { } organisationId
            ? await access.ForOrganisationAsync(organisationId, request.ProgrammeId, cancellationToken)
            : await access.ForStaffAsync(request.ProgrammeId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<PublishCampaignResponse>(loaded.Error);
        }

        var programme = loaded.Value;
        var now = clock.UtcNow;
        var published = programme.Publish(currentUser.UserId ?? Guid.Empty, now);
        if (published.IsFailure)
        {
            return Result.Failure<PublishCampaignResponse>(published.Error);
        }

        var existing = await db.Seats.CountAsync(s => s.ProgrammeId == programme.Id, cancellationToken);
        for (var n = existing + 1; n <= published.Value; n++)
        {
            db.Seats.Add(Seat.Open(programme.TenantId, programme.Id, n));
        }

        if (!await db.Funnels.AnyAsync(f => f.ProgrammeId == programme.Id, cancellationToken))
        {
            db.Funnels.Add(ProgrammeFunnel.Start(programme.TenantId, programme.Id, now));
        }

        outbox.Enqueue(new ProgrammePublished(programme.Id, programme.Id, programme.OrganisationId, programme.Name, programme.ApplicationsCloseAt!.Value));
        return new PublishCampaignResponse(programme.Id, programme.Status.ToString().ToLowerInvariant(), published.Value, now);
    }
}

internal static class PublishCampaignEndpoint
{
    public static void Map(IEndpointRouteBuilder sponsor, IEndpointRouteBuilder admin)
    {
        sponsor.MapPost("/{programmeId:guid}/publish", async (Guid organisationId, Guid programmeId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new PublishCampaignCommand(organisationId, programmeId), ct)).ToHttpResult())
            .WithName("PublishCampaign")
            .WithSummary("Sponsor owner: publish the opportunity (needs an approved quote, programme review and funding or an approved payment plan).")
            .RequireAuthorization(IdentityPolicies.OrganisationOwner);

        admin.MapPost("/{programmeId:guid}/publish", async (Guid programmeId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new PublishCampaignCommand(null, programmeId), ct)).ToHttpResult())
            .WithName("PublishCampaignAsStaff")
            .WithSummary("Staff: publish on the sponsor's behalf (same gates).")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
