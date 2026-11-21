using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Tutors.Contracts;
using Plastoids.Modules.Tutors.Persistence;

namespace Plastoids.Modules.Tutors.Services;

internal sealed class TutorPayoutAccounts(TutorsDbContext db, IUnitOfWork unitOfWork, IFieldEncryptor encryptor) : ITutorPayoutAccounts
{
    public async Task<TutorPayoutDestination?> GetAsync(Guid tutorUserId, CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }

        var row = await db.Profiles.AsNoTracking().Where(p => p.UserId == tutorUserId)
            .Join(db.PayoutAccounts.AsNoTracking(), p => p.Id, a => a.TutorProfileId, (p, a) => a)
            .SingleOrDefaultAsync(cancellationToken);
        return row is null ? null : new TutorPayoutDestination(tutorUserId, row.BankCode, encryptor.Decrypt(row.AccountNumberCiphertext), row.AccountNumberMasked, row.AccountName, row.NameMatched);
    }
}
