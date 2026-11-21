using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Identity.Domain;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Identity.Features.Organisations.ChangeMemberRole;

internal sealed class ChangeMemberRoleHandler(IdentityDbContext db, ICurrentUser currentUser) : ICommandHandler<ChangeMemberRoleCommand>
{
    public async Task<Result> Handle(ChangeMemberRoleCommand request, CancellationToken cancellationToken)
    {
        var membership = await db.Memberships.SingleOrDefaultAsync(m => m.OrganisationId == request.OrganisationId && m.UserId == request.UserId, cancellationToken);
        if (membership is null)
        {
            return Error.NotFound("member_not_found", "That user is not a member of the organisation.");
        }

        if (membership.Role == OrganisationRoles.Owner && request.Role != OrganisationRoles.Owner)
        {
            var owners = await db.Memberships.CountAsync(m => m.OrganisationId == request.OrganisationId && m.Role == OrganisationRoles.Owner, cancellationToken);
            if (owners <= 1)
            {
                return Error.Conflict("last_owner", "An organisation must keep at least one owner.");
            }
        }

        membership.ChangeRole(request.Role, currentUser.UserId ?? Guid.Empty);
        return Result.Success();
    }
}
