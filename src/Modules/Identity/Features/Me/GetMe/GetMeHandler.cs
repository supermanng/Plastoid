using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Identity.Domain;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Identity.Features.Me.GetMe;

internal sealed class GetMeHandler(IdentityDbContext db, IFieldEncryptor encryptor, ICurrentUser currentUser) : IQueryHandler<GetMeQuery, MeResponse>
{
    public async Task<Result<MeResponse>> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var memberships = await db.Memberships.AsNoTracking().Where(m => m.UserId == user.Id).ToListAsync(cancellationToken);
        var organisationIds = memberships.Where(m => m.OrganisationId is not null).Select(m => m.OrganisationId!.Value).ToList();
        var organisations = await db.Organisations.AsNoTracking().Where(o => organisationIds.Contains(o.Id)).ToListAsync(cancellationToken);

        return new MeResponse(
            user.Id,
            PhoneNumber.Mask(encryptor.Decrypt(user.PhoneCiphertext)),
            user.Email,
            user.EmailVerifiedAt is not null,
            user.DisplayName,
            user.MfaEnabled,
            user.NinVerifiedAt is not null,
            memberships.Where(m => m.IsPlatformRole).Select(m => m.Role).Distinct().ToList(),
            memberships.Where(m => m.OrganisationId is not null)
                .Join(organisations, m => m.OrganisationId, o => o.Id, (m, o) => new MeOrganisation(o.Id, o.Name, o.Type.ToString().ToLowerInvariant(), m.Role, o.VerificationStatus.ToString().ToLowerInvariant()))
                .ToList(),
            user.CareerGoal,
            user.Interests,
            user.CreatedAt);
    }
}
