using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Applications.Domain;
using Plastoids.Modules.Applications.Persistence;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Applications.Features.Review.ApproveShortlist;

/// <summary>Shortlist → sponsor approval (owner of the sponsoring organisation) or Plastoids staff when selection is delegated → offers.</summary>
public sealed record ApproveShortlistCommand(Guid ProgrammeId) : ICommand<ApproveShortlistResponse>;

public sealed record ApproveShortlistResponse(Guid ProgrammeId, int AcceptedCount, bool Delegated, DateTime ApprovedAt);

internal sealed class ApproveShortlistValidator : AbstractValidator<ApproveShortlistCommand>
{
    public ApproveShortlistValidator()
    {
        RuleFor(x => x.ProgrammeId).NotEmpty();
    }
}

internal sealed class ApproveShortlistHandler(ApplicationsDbContext db, IProgrammeReader programmes, IOrganisationDirectory organisations, ICurrentUser currentUser, ICurrentTenant tenant, IClock clock) : ICommandHandler<ApproveShortlistCommand, ApproveShortlistResponse>
{
    public async Task<Result<ApproveShortlistResponse>> Handle(ApproveShortlistCommand request, CancellationToken cancellationToken)
    {
        var programme = await programmes.GetAsync(request.ProgrammeId, cancellationToken);
        if (programme is null)
        {
            return Error.NotFound("programme_not_found", "No such programme.");
        }

        var userId = currentUser.UserId ?? Guid.Empty;
        var isStaff = currentUser.Roles.Contains("staff");
        var isOwner = await organisations.IsMemberAsync(userId, programme.OrganisationId, ["owner"], cancellationToken);
        var delegated = false;
        if (!isOwner)
        {
            if (!(isStaff && programme.SelectionMode == "delegated"))
            {
                return Error.Forbidden("shortlist_approval_not_allowed", "Only the sponsoring organisation's owner (or Plastoids when selection is delegated) can approve the shortlist.");
            }

            delegated = true;
        }

        var existing = await db.ShortlistApprovals.SingleOrDefaultAsync(s => s.ProgrammeId == programme.ProgrammeId, cancellationToken);
        var accepted = await db.Applications.CountAsync(a => a.ProgrammeId == programme.ProgrammeId && a.Status == ApplicationStatus.Accepted, cancellationToken);
        if (existing is not null)
        {
            return new ApproveShortlistResponse(programme.ProgrammeId, accepted, existing.Delegated, existing.ApprovedAt);
        }

        if (accepted == 0)
        {
            return Error.Conflict("shortlist_empty", "No accepted applications to approve yet.");
        }

        var approval = ShortlistApproval.Record(tenant.TenantId, programme.ProgrammeId, userId, delegated, accepted, clock.UtcNow);
        db.ShortlistApprovals.Add(approval);
        return new ApproveShortlistResponse(programme.ProgrammeId, accepted, delegated, approval.ApprovedAt);
    }
}

internal static class ApproveShortlistEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/programmes/{programmeId:guid}/shortlist/approve", async (Guid programmeId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ApproveShortlistCommand(programmeId), ct)).ToHttpResult())
            .WithName("ApproveShortlist")
            .WithSummary("Sponsor owner (or staff when delegated): sign off the accepted shortlist so offers can go out.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
