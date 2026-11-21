using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Identity.Domain;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Identity.Features.Organisations.RegisterOrganisation;

internal sealed class RegisterOrganisationHandler(IdentityDbContext db, ICurrentUser currentUser, ICurrentTenant tenant) : ICommandHandler<RegisterOrganisationCommand, RegisterOrganisationResponse>
{
    public async Task<Result<RegisterOrganisationResponse>> Handle(RegisterOrganisationCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var type = Enum.Parse<OrganisationType>(request.Type, true);
        var organisation = Organisation.Register(tenant.TenantId, request.Name, type, request.CacNumber, request.SignatoryName, request.SignatoryEmail);
        if (await db.Organisations.AnyAsync(o => o.CacNumber == organisation.CacNumber, cancellationToken))
        {
            return Error.Conflict("cac_number_in_use", "An organisation with this CAC number already exists.");
        }

        db.Organisations.Add(organisation);
        db.Memberships.Add(Membership.Organisation(tenant.TenantId, user.Id, organisation.Id, OrganisationRoles.Owner, user.Id));

        var platformRole = type == OrganisationType.Sponsor || type == OrganisationType.Employer ? RoleNames.Sponsor : null;
        if (platformRole is not null && !await db.Memberships.AnyAsync(m => m.UserId == user.Id && m.OrganisationId == null && m.Role == platformRole, cancellationToken))
        {
            db.Memberships.Add(Membership.Platform(tenant.TenantId, user.Id, platformRole, user.Id));
        }

        return new RegisterOrganisationResponse(organisation.Id, organisation.VerificationStatus.ToString().ToLowerInvariant());
    }
}
