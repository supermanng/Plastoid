using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.Modules.Identity.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Identity.Features.Auth.IssueToken;

internal sealed class IssueTokenHandler(
    IdentityDbContext db,
    TokenIssuer tokens,
    ITokenService tokenService,
    IPasswordHasher hasher,
    IGoogleIdTokenVerifier google) : ICommandHandler<IssueTokenCommand, TokenResponse>
{
    private static readonly Error InvalidCredentials = Error.Unauthorized("invalid_credentials", "Email or password is incorrect.");

    public async Task<Result<TokenResponse>> Handle(IssueTokenCommand request, CancellationToken cancellationToken)
        => request.GrantType switch
        {
            GrantTypes.Password => await PasswordAsync(request.Email!, request.Password!, cancellationToken),
            GrantTypes.RefreshToken => await tokens.RotateAsync(request.RefreshToken!, cancellationToken),
            GrantTypes.Google => await GoogleAsync(request.IdToken!, cancellationToken),
            _ => Error.Validation("unsupported_grant_type", "Unsupported grant type."),
        };

    private async Task<Result<TokenResponse>> PasswordAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalised = email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == normalised, cancellationToken);
        if (user?.PasswordHash is null)
        {
            // Burn the same time as a real check so timing does not reveal whether the email exists.
            hasher.Verify(hasher.Hash("plastoids-dummy-password"), password);
            return InvalidCredentials;
        }

        var verification = hasher.Verify(user.PasswordHash, password);
        if (verification == PasswordVerification.Failed)
        {
            return InvalidCredentials;
        }

        if (!user.IsActive)
        {
            return Error.Forbidden("account_inactive", "This account is not active.");
        }

        if (verification == PasswordVerification.SuccessRehashNeeded)
        {
            user.UpdatePassword(hasher.Hash(password));
        }

        if (user.MfaEnabled)
        {
            return TokenResponse.MfaChallenge(tokenService.IssueMfaChallenge(user.Id, user.TenantId).Token);
        }

        return await tokens.IssueAsync(user, [PlastoidsClaims.AmrPassword], cancellationToken);
    }

    private async Task<Result<TokenResponse>> GoogleAsync(string idToken, CancellationToken cancellationToken)
    {
        var identity = await google.VerifyAsync(idToken, cancellationToken);
        if (identity.IsFailure)
        {
            return Result.Failure<TokenResponse>(identity.Error);
        }

        if (!identity.Value.EmailVerified)
        {
            return Error.Unauthorized("google_email_unverified", "The Google account email is not verified.");
        }

        var email = identity.Value.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);
        if (user is null)
        {
            return Error.NotFound("account_not_found", "No Plastoids account uses this Google email; sign up with your phone first.");
        }

        if (!user.IsActive)
        {
            return Error.Forbidden("account_inactive", "This account is not active.");
        }

        if (user.MfaEnabled)
        {
            return TokenResponse.MfaChallenge(tokenService.IssueMfaChallenge(user.Id, user.TenantId).Token);
        }

        return await tokens.IssueAsync(user, ["google"], cancellationToken);
    }
}
