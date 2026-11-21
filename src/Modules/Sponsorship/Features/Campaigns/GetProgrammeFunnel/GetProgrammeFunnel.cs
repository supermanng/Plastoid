using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Identity.Authorization;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Sponsorship.Features.Campaigns.GetProgrammeFunnel;

/// <summary>Story F5: the funnel is refreshed from events as they arrive (well within the hourly bound). CSV on request.</summary>
public sealed record GetProgrammeFunnelQuery(Guid OrganisationId, Guid ProgrammeId) : IQuery<FunnelView>;

internal sealed class GetProgrammeFunnelValidator : AbstractValidator<GetProgrammeFunnelQuery>
{
    public GetProgrammeFunnelValidator()
    {
        RuleFor(x => x.ProgrammeId).NotEmpty();
    }
}

internal sealed class GetProgrammeFunnelHandler(SponsorshipDbContext db, ICurrentUser currentUser) : IQueryHandler<GetProgrammeFunnelQuery, FunnelView>
{
    public async Task<Result<FunnelView>> Handle(GetProgrammeFunnelQuery request, CancellationToken cancellationToken)
    {
        var programme = await db.Programmes.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.ProgrammeId, cancellationToken);
        if (programme is null || (programme.OrganisationId != request.OrganisationId && !currentUser.Roles.Contains("staff")))
        {
            return Error.NotFound("programme_not_found", "No such programme for this organisation.");
        }

        return await FunnelReader.BuildAsync(db, programme, cancellationToken);
    }
}

internal static class GetProgrammeFunnelEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/{programmeId:guid}/funnel", async (Guid organisationId, Guid programmeId, string? format, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new GetProgrammeFunnelQuery(organisationId, programmeId), ct);
                if (result.IsFailure)
                {
                    return ResultHttp.Problem(result.Error);
                }

                return string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase)
                    ? Results.Text(result.Value.ToCsv(), "text/csv", Encoding.UTF8)
                    : Results.Ok(result.Value);
            })
            .WithName("GetProgrammeFunnel")
            .WithSummary("Sponsor: funnel and attendance (event-driven, refreshed continuously); ?format=csv exports it.")
            .RequireAuthorization(IdentityPolicies.OrganisationMember);
    }
}
