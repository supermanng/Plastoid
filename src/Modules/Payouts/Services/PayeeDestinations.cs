using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payouts.Persistence;
using Plastoids.Modules.Tutors.Contracts;

namespace Plastoids.Modules.Payouts.Services;

/// <summary>Where a payout goes. AccountNumber is the full number: read at submission time only, never stored on the payout, never logged.</summary>
public sealed record PayeeDestination(string BankCode, string AccountNumber, string AccountNumberMasked, string AccountName, bool NameMatched);

/// <summary>Resolves a payee's bank account: tutors from Tutors' payout accounts, organisations from this module's (ADR 0019).</summary>
public sealed class PayeeDestinations(PayoutsDbContext db, ITutorPayoutAccounts tutorAccounts, IFieldEncryptor encryptor)
{
    public async Task<PayeeDestination?> GetAsync(string payeeType, Guid payeeId, CancellationToken ct)
    {
        if (payeeType == PayeeTypes.Organisation)
        {
            var account = await db.OrganisationAccounts.AsNoTracking().SingleOrDefaultAsync(a => a.OrganisationId == payeeId, ct);
            return account is null ? null : new PayeeDestination(account.BankCode, encryptor.Decrypt(account.AccountNumberCiphertext), account.AccountNumberMasked, account.AccountName, account.NameMatched);
        }

        var tutor = await tutorAccounts.GetAsync(payeeId, ct);
        return tutor is null ? null : new PayeeDestination(tutor.BankCode, tutor.AccountNumber, tutor.AccountNumberMasked, tutor.AccountName, tutor.NameMatched);
    }
}
