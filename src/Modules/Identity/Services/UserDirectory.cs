using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Identity.Persistence;

namespace Plastoids.Modules.Identity.Services;

internal sealed class UserDirectory(IdentityDbContext db, IUnitOfWork unitOfWork) : IUserDirectory
{
    public async Task<UserSummary?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }

        return await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new UserSummary(u.Id, u.DisplayName, u.Email, u.Status == Domain.UserStatus.Active)).SingleOrDefaultAsync(cancellationToken);
    }
}
