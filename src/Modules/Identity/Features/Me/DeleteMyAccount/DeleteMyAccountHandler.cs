using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.Modules.Identity.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Identity.Features.Me.DeleteMyAccount;

internal sealed class DeleteMyAccountHandler(IdentityDbContext db, TokenIssuer tokens, ICurrentUser currentUser, IClock clock) : ICommandHandler<DeleteMyAccountCommand>
{
    public async Task<Result> Handle(DeleteMyAccountCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        user.Anonymise(clock.UtcNow);
        await tokens.RevokeAllAsync(user.Id, cancellationToken);
        db.MfaRecoveryCodes.RemoveRange(await db.MfaRecoveryCodes.Where(c => c.UserId == user.Id).ToListAsync(cancellationToken));
        db.Memberships.RemoveRange(await db.Memberships.Where(m => m.UserId == user.Id && m.OrganisationId != null).ToListAsync(cancellationToken));
        return Result.Success();
    }
}
