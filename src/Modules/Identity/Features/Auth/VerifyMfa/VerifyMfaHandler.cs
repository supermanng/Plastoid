using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.Modules.Identity.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Identity.Features.Auth.VerifyMfa;

internal sealed class VerifyMfaHandler(
    IdentityDbContext db,
    TokenIssuer tokens,
    ITokenService tokenService,
    IFieldEncryptor encryptor,
    ICurrentUser currentUser,
    IClock clock) : ICommandHandler<VerifyMfaCommand, TokenResponse>
{
    public async Task<Result<TokenResponse>> Handle(VerifyMfaCommand request, CancellationToken cancellationToken)
    {
        Guid? userId;
        string primaryAmr;
        if (!string.IsNullOrWhiteSpace(request.MfaToken))
        {
            userId = tokenService.ValidateMfaChallenge(request.MfaToken);
            primaryAmr = PlastoidsClaims.AmrPassword;
            if (userId is null)
            {
                return Error.Unauthorized("mfa_token_invalid", "The MFA challenge expired; sign in again.");
            }
        }
        else
        {
            userId = currentUser.UserId;
            primaryAmr = PlastoidsClaims.AmrOtp;
            if (userId is null)
            {
                return Error.Unauthorized("not_signed_in", "Sign in, or send the mfaToken from the login response.");
            }
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Error.Forbidden("account_inactive", "This account is not active.");
        }

        if (user.MfaSecretCiphertext is null)
        {
            return Error.Validation("mfa_not_enrolled", "Start MFA enrolment first.");
        }

        var secret = Base32.Decode(encryptor.Decrypt(user.MfaSecretCiphertext));
        var code = request.Code.Trim();
        var ok = Totp.Verify(secret, code, clock.UtcNow);
        if (!ok && user.MfaEnabled)
        {
            var hash = RecoveryCodes.Hash(code);
            var recovery = await db.MfaRecoveryCodes.SingleOrDefaultAsync(c => c.UserId == user.Id && c.CodeHash == hash && c.UsedAt == null, cancellationToken);
            if (recovery is not null)
            {
                recovery.Use(clock.UtcNow);
                ok = true;
            }
        }

        if (!ok)
        {
            return Error.Unauthorized("mfa_code_invalid", "The code is not valid.");
        }

        if (!user.MfaEnabled)
        {
            var confirmed = user.ConfirmMfa();
            if (confirmed.IsFailure)
            {
                return Result.Failure<TokenResponse>(confirmed.Error);
            }
        }

        return await tokens.IssueAsync(user, [primaryAmr, PlastoidsClaims.AmrMfa], cancellationToken);
    }
}
