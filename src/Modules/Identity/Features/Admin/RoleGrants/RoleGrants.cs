using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Identity.Domain;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Identity.Features.Admin.RoleGrants;

/// <summary>Four-eyes role grants (blueprint §19): propose, then a different staff member approves; the grant lands through IPlatformRoles.</summary>
public sealed record ProposeRoleGrantCommand(Guid UserId, string Role, string Reason, string IdempotencyKey) : ICommand<RoleGrantView>, IIdempotentCommand;

public sealed record ProposeRoleGrantRequest(Guid UserId, string Role, string Reason);

public sealed record DecideRoleGrantCommand(Guid ProposalId, string Decision, string? Note, string IdempotencyKey) : ICommand<RoleGrantView>, IIdempotentCommand;

public sealed record DecideRoleGrantRequest(string Decision, string? Note);

public sealed record ListRoleGrantsQuery(string? Status) : IQuery<IReadOnlyList<RoleGrantView>>;

public sealed record RoleGrantView(Guid ProposalId, Guid UserId, string Role, string Reason, string Status, Guid ProposedBy, DateTime ProposedAt, Guid? DecidedBy, DateTime? DecidedAt, string? DecisionNote)
{
    public static RoleGrantView From(RoleGrantProposal p) => new(p.Id, p.UserId, p.Role, p.Reason, p.Status.ToString().ToLowerInvariant(), p.ProposedBy, p.ProposedAt, p.DecidedBy, p.DecidedAt, p.DecisionNote);
}

internal sealed class ProposeRoleGrantValidator : AbstractValidator<ProposeRoleGrantCommand>
{
    public ProposeRoleGrantValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Role).NotEmpty().MaximumLength(16);
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class DecideRoleGrantValidator : AbstractValidator<DecideRoleGrantCommand>
{
    public DecideRoleGrantValidator()
    {
        RuleFor(x => x.ProposalId).NotEmpty();
        RuleFor(x => x.Decision).Must(d => d is "approved" or "rejected").WithMessage("decision must be approved or rejected.");
        RuleFor(x => x.Note).NotEmpty().MaximumLength(1000).When(x => x.Decision == "rejected");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class ProposeRoleGrantHandler(IdentityDbContext db, ICurrentUser currentUser, ICurrentTenant tenant, IClock clock) : ICommandHandler<ProposeRoleGrantCommand, RoleGrantView>
{
    public async Task<Result<RoleGrantView>> Handle(ProposeRoleGrantCommand request, CancellationToken cancellationToken)
    {
        if (!await db.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken))
        {
            return Error.NotFound("user_not_found", "No such user.");
        }

        var role = request.Role.Trim().ToLowerInvariant();
        if (await db.Memberships.AnyAsync(m => m.UserId == request.UserId && m.OrganisationId == null && m.Role == role, cancellationToken))
        {
            return Error.Conflict("role_already_granted", "The user already holds that role.");
        }

        if (await db.RoleGrants.AnyAsync(g => g.UserId == request.UserId && g.Role == role && g.Status == RoleGrantStatus.Proposed, cancellationToken))
        {
            return Error.Conflict("grant_already_proposed", "A proposal for that role is already awaiting approval.");
        }

        var proposal = RoleGrantProposal.Propose(tenant.TenantId, request.UserId, role, request.Reason, currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        if (proposal.IsFailure)
        {
            return Result.Failure<RoleGrantView>(proposal.Error);
        }

        db.RoleGrants.Add(proposal.Value);
        return RoleGrantView.From(proposal.Value);
    }
}

internal sealed class DecideRoleGrantHandler(IdentityDbContext db, IPlatformRoles roles, ICurrentUser currentUser, IClock clock) : ICommandHandler<DecideRoleGrantCommand, RoleGrantView>
{
    public async Task<Result<RoleGrantView>> Handle(DecideRoleGrantCommand request, CancellationToken cancellationToken)
    {
        var proposal = await db.RoleGrants.SingleOrDefaultAsync(g => g.Id == request.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return Error.NotFound("grant_not_found", "No such role grant proposal.");
        }

        var staffId = currentUser.UserId ?? Guid.Empty;
        var now = clock.UtcNow;
        var decided = request.Decision == "approved" ? proposal.Approve(staffId, now) : proposal.Reject(staffId, request.Note ?? string.Empty, now);
        if (decided.IsFailure)
        {
            return Result.Failure<RoleGrantView>(decided.Error);
        }

        if (proposal.Status == RoleGrantStatus.Approved)
        {
            await roles.GrantAsync(proposal.UserId, proposal.Role, staffId, cancellationToken);
        }

        return RoleGrantView.From(proposal);
    }
}

internal sealed class ListRoleGrantsHandler(IdentityDbContext db) : IQueryHandler<ListRoleGrantsQuery, IReadOnlyList<RoleGrantView>>
{
    public async Task<Result<IReadOnlyList<RoleGrantView>>> Handle(ListRoleGrantsQuery request, CancellationToken cancellationToken)
    {
        var query = db.RoleGrants.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<RoleGrantStatus>(request.Status, true, out var parsed))
        {
            query = query.Where(g => g.Status == parsed);
        }

        var rows = await query.OrderByDescending(g => g.ProposedAt).Take(200).ToListAsync(cancellationToken);
        return rows.Select(RoleGrantView.From).ToList();
    }
}

internal static class RoleGrantEndpoints
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapPost("/role-grants", async (ProposeRoleGrantRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new ProposeRoleGrantCommand(body.UserId, body.Role, body.Reason, key), ct)).ToCreatedResult(r => $"/api/v1/admin/identity/role-grants/{r.ProposalId}");
            })
            .WithName("ProposeRoleGrant")
            .WithSummary("Staff (maker): propose a platform role grant (staff | tutor | sponsor). Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);

        admin.MapPost("/role-grants/{proposalId:guid}/decision", async (Guid proposalId, DecideRoleGrantRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new DecideRoleGrantCommand(proposalId, body.Decision, body.Note, key), ct)).ToHttpResult();
            })
            .WithName("DecideRoleGrant")
            .WithSummary("Staff (checker ≠ maker): approve or reject the grant. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);

        admin.MapGet("/role-grants", async (string? status, ISender sender, CancellationToken ct) => (await sender.Send(new ListRoleGrantsQuery(status), ct)).ToHttpResult())
            .WithName("ListRoleGrants")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
