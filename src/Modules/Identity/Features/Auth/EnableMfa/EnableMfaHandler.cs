using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Identity.Domain;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.Modules.Identity.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Identity.Features.Auth.EnableMfa;

internal sealed class EnableMfaHandler(IdentityDbContext db, IFieldEncryptor encryptor, ICurrentUser currentUser) : ICommandHandler<EnableMfaCommand, EnableMfaResponse>
{
    public const string Issuer = "Plastoids";

    public async Task<Result<EnableMfaResponse>> Handle(EnableMfaCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken);
        if (user is null)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var secret = Totp.GenerateSecret();
        var started = user.BeginMfaEnrolment(encryptor.Encrypt(Base32.Encode(secret)));
        if (started.IsFailure)
        {
            return Result.Failure<EnableMfaResponse>(started.Error);
        }

        var old = await db.MfaRecoveryCodes.Where(c => c.UserId == user.Id).ToListAsync(cancellationToken);
        db.MfaRecoveryCodes.RemoveRange(old);
        var codes = RecoveryCodes.Generate();
        db.MfaRecoveryCodes.AddRange(codes.Select(c => MfaRecoveryCode.Create(user.TenantId, user.Id, RecoveryCodes.Hash(c))));

        var account = user.Email ?? PhoneNumber.Mask(encryptor.Decrypt(user.PhoneCiphertext));
        return new EnableMfaResponse(Base32.Encode(secret), Totp.OtpAuthUri(Issuer, account, secret), codes);
    }
}
