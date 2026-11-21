using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Security;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Identity.Domain;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.Modules.Identity.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Identity.Features.Auth.VerifyOtp;

internal sealed class VerifyOtpHandler(
    IdentityDbContext db,
    OtpService otp,
    TokenIssuer tokens,
    IFieldEncryptor encryptor,
    IOutbox outbox,
    ICurrentTenant tenant,
    IClock clock) : ICommandHandler<VerifyOtpCommand, VerifyOtpResponse>
{
    public async Task<Result<VerifyOtpResponse>> Handle(VerifyOtpCommand request, CancellationToken cancellationToken)
    {
        PhoneNumber.TryNormalise(request.Phone, out var phone);
        var verified = await otp.VerifyAsync(phone, request.Code);
        if (verified.IsFailure)
        {
            return Result.Failure<VerifyOtpResponse>(verified.Error);
        }

        var phoneHash = encryptor.BlindIndex(phone);
        var user = await db.Users.SingleOrDefaultAsync(u => u.PhoneHash == phoneHash, cancellationToken);
        if (user is not null)
        {
            if (!user.IsActive)
            {
                return Error.Forbidden("account_inactive", "This account is not active.");
            }

            var existingTokens = await tokens.IssueAsync(user, [PlastoidsClaims.AmrOtp], cancellationToken);
            return new VerifyOtpResponse(false, user.Id, existingTokens);
        }

        // Story A1: the account exists only after verification, and only for adults.
        if (request.DateOfBirth is null || string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return Error.Validation("registration_details_required", "This number is new: send dateOfBirth and displayName to create the account.");
        }

        var created = User.Register(
            tenant.TenantId,
            encryptor.Encrypt(phone),
            phoneHash,
            encryptor.Encrypt(request.DateOfBirth.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)),
            request.DateOfBirth.Value,
            request.DisplayName,
            clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<VerifyOtpResponse>(created.Error);
        }

        user = created.Value;
        db.Users.Add(user);
        db.Memberships.Add(Membership.Platform(tenant.TenantId, user.Id, RoleNames.Learner));
        outbox.Enqueue(new UserRegistered(user.Id, user.Id, phoneHash, RoleNames.Learner));

        var newTokens = await tokens.IssueAsync(user, [PlastoidsClaims.AmrOtp], cancellationToken);
        return new VerifyOtpResponse(true, user.Id, newTokens);
    }
}
