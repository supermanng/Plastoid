using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Identity.Domain;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Identity.Services;

internal sealed class PlatformRoles(IdentityDbContext db, IUnitOfWork unitOfWork, ICurrentTenant tenant) : IPlatformRoles
{
    public async Task GrantAsync(Guid userId, string role, Guid? grantedBy, CancellationToken cancellationToken)
    {
        await unitOfWork.EnlistAsync(db, cancellationToken);
        if (await db.Memberships.AnyAsync(m => m.UserId == userId && m.OrganisationId == null && m.Role == role, cancellationToken))
        {
            return;
        }

        db.Memberships.Add(Membership.Platform(tenant.TenantId, userId, role, grantedBy));
    }

    public Task<bool> HasAsync(Guid userId, string role, CancellationToken cancellationToken)
        => db.Memberships.AnyAsync(m => m.UserId == userId && m.OrganisationId == null && m.Role == role, cancellationToken);
}
