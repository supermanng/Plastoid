using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Identity.Domain;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Identity.Features.Organisations.AddMember;

internal sealed class AddMemberHandler(IdentityDbContext db, IFieldEncryptor encryptor, ICurrentUser currentUser) : ICommandHandler<AddMemberCommand, AddMemberResponse>
{
    public async Task<Result<AddMemberResponse>> Handle(AddMemberCommand request, CancellationToken cancellationToken)
    {
        var organisation = await db.Organisations.AsNoTracking().SingleOrDefaultAsync(o => o.Id == request.OrganisationId, cancellationToken);
        if (organisation is null)
        {
            return Error.NotFound("organisation_not_found", "No such organisation.");
        }

        User? member = null;
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var email = request.Email.Trim().ToLowerInvariant();
            member = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
        }
        else if (PhoneNumber.TryNormalise(request.Phone, out var phone))
        {
            var hash = encryptor.BlindIndex(phone);
            member = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.PhoneHash == hash, cancellationToken);
        }

        if (member is null || !member.IsActive)
        {
            return Error.NotFound("member_not_found", "No active Plastoids account matches; ask them to sign up first.");
        }

        if (await db.Memberships.AnyAsync(m => m.UserId == member.Id && m.OrganisationId == organisation.Id, cancellationToken))
        {
            return Error.Conflict("already_member", "This user already belongs to the organisation; change their role instead.");
        }

        db.Memberships.Add(Membership.Organisation(organisation.TenantId, member.Id, organisation.Id, request.Role, currentUser.UserId));
        return new AddMemberResponse(organisation.Id, member.Id, request.Role);
    }
}
