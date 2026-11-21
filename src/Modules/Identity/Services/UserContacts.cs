using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Identity.Persistence;

namespace Plastoids.Modules.Identity.Services;

/// <summary>Implements <see cref="IUserContacts"/> for Notifications; joins the caller's unit of work.</summary>
internal sealed class UserContacts(IdentityDbContext db, IUnitOfWork unitOfWork, IFieldEncryptor encryptor) : IUserContacts
{
    public async Task<UserContact?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }

        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.Id, u.DisplayName, u.Email, u.Status, u.PhoneCiphertext }).SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        var phone = user.PhoneCiphertext.Length > 0 ? encryptor.Decrypt(user.PhoneCiphertext) : null;
        return new UserContact(user.Id, user.DisplayName, phone, user.Email, user.Status == Domain.UserStatus.Active);
    }
}
