using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Tutors.Domain;
using Plastoids.Modules.Tutors.Persistence;
using Plastoids.Modules.Tutors.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Tutors.Features.Profile.SetPayoutAccount;

internal sealed class SetPayoutAccountHandler(TutorsDbContext db, TutorAccess access, IBankAccountResolver resolver, IFieldEncryptor encryptor, IClock clock)
    : ICommandHandler<SetPayoutAccountCommand, SetPayoutAccountResponse>
{
    public async Task<Result<SetPayoutAccountResponse>> Handle(SetPayoutAccountCommand request, CancellationToken cancellationToken)
    {
        var mine = await access.MineAsync(cancellationToken);
        if (mine.IsFailure)
        {
            return Result.Failure<SetPayoutAccountResponse>(mine.Error);
        }

        var profile = mine.Value;
        if (!profile.IdentityPassed || profile.VerifiedFullName is null)
        {
            return Error.Validation("identity_required", "Verify your identity before adding a payout account (the bank name must match it).");
        }

        var resolved = await resolver.ResolveAsync(request.BankCode, request.AccountNumber, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<SetPayoutAccountResponse>(resolved.Error);
        }

        var matched = PayoutAccount.NamesMatch(profile.VerifiedFullName, resolved.Value.AccountName);
        if (!matched)
        {
            return Error.Validation("payout_name_mismatch", "The bank account name does not match your verified identity.");
        }

        var masked = "******" + request.AccountNumber[^4..];
        var cipher = encryptor.Encrypt(request.AccountNumber);
        var existing = await db.PayoutAccounts.SingleOrDefaultAsync(a => a.TutorProfileId == profile.Id, cancellationToken);
        if (existing is null)
        {
            db.PayoutAccounts.Add(PayoutAccount.Create(profile.TenantId, profile.Id, request.BankCode, cipher, masked, resolved.Value.AccountName, true, clock.UtcNow));
        }
        else
        {
            existing.Replace(request.BankCode, cipher, masked, resolved.Value.AccountName, true, clock.UtcNow);
        }

        return new SetPayoutAccountResponse(request.BankCode, masked, resolved.Value.AccountName, true);
    }
}
