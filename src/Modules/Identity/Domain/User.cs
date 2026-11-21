using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Identity.Domain;

public enum UserStatus
{
    Active = 0,
    Suspended = 1,
    Deleted = 2,
}

/// <summary>
/// An account (blueprint §16 identity.User). Phone and date of birth are stored encrypted; phone_hash is a blind index
/// used for the one-account-per-phone rule. The account only exists once the phone OTP was verified.
/// </summary>
[Audited]
public sealed class User : Entity, ISoftDeletable
{
    public const int MinimumAge = 18;

    private User()
    {
    }

    private User(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public string PhoneCiphertext { get; private set; } = string.Empty;
    public string PhoneHash { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public DateTime? EmailVerifiedAt { get; private set; }
    public string? PasswordHash { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string DobCiphertext { get; private set; } = string.Empty;
    public bool MfaEnabled { get; private set; }
    public string? MfaSecretCiphertext { get; private set; }
    public UserStatus Status { get; private set; }
    public DateTime? NinVerifiedAt { get; private set; }
    public string? CareerGoal { get; private set; }
    public List<string> Interests { get; private set; } = [];
    public DateTime? LastLoginAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    /// <summary>Registers a verified phone. Fails for under-18s (blueprint §19 age gate: adults only at MVP).</summary>
    public static Result<User> Register(
        Guid tenantId,
        string phoneCiphertext,
        string phoneHash,
        string dobCiphertext,
        DateOnly dateOfBirth,
        string displayName,
        DateTime utcNow)
    {
        var today = DateOnly.FromDateTime(utcNow);
        var age = today.Year - dateOfBirth.Year - (today < dateOfBirth.AddYears(today.Year - dateOfBirth.Year) ? 1 : 0);
        if (dateOfBirth > today)
        {
            return Error.Validation("dob_in_future", "Date of birth cannot be in the future.");
        }

        if (age < MinimumAge)
        {
            return Error.Validation("under_18", "You must be at least 18 to use Plastoids.");
        }

        return new User(NewId(), tenantId)
        {
            PhoneCiphertext = phoneCiphertext,
            PhoneHash = phoneHash,
            DobCiphertext = dobCiphertext,
            DisplayName = displayName.Trim(),
            Status = UserStatus.Active,
        };
    }

    public Result SetEmailAndPassword(string email, string passwordHash)
    {
        if (Status != UserStatus.Active)
        {
            return Error.Forbidden("account_inactive", "This account is not active.");
        }

        Email = email.Trim().ToLowerInvariant();
        EmailVerifiedAt = null;
        PasswordHash = passwordHash;
        return Result.Success();
    }

    public void UpdatePassword(string passwordHash) => PasswordHash = passwordHash;

    public Result UpdateProfile(string? displayName, string? email)
    {
        if (Status != UserStatus.Active)
        {
            return Error.Forbidden("account_inactive", "This account is not active.");
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = displayName.Trim();
        }

        if (email is not null)
        {
            var normalised = email.Trim().ToLowerInvariant();
            if (!string.Equals(normalised, Email, StringComparison.Ordinal))
            {
                Email = normalised;
                EmailVerifiedAt = null;
            }
        }

        return Result.Success();
    }

    public void UpdateGoals(string? careerGoal, IEnumerable<string> interests)
    {
        CareerGoal = string.IsNullOrWhiteSpace(careerGoal) ? null : careerGoal.Trim();
        Interests = interests.Select(i => i.Trim()).Where(i => i.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();
    }

    /// <summary>Starts MFA enrolment: the secret is stored but MFA is not enforced until <see cref="ConfirmMfa"/>.</summary>
    public Result BeginMfaEnrolment(string secretCiphertext)
    {
        if (MfaEnabled)
        {
            return Error.Conflict("mfa_already_enabled", "MFA is already enabled; disable it before re-enrolling.");
        }

        MfaSecretCiphertext = secretCiphertext;
        return Result.Success();
    }

    public Result ConfirmMfa()
    {
        if (MfaSecretCiphertext is null)
        {
            return Error.Validation("mfa_not_enrolled", "Start MFA enrolment first.");
        }

        MfaEnabled = true;
        return Result.Success();
    }

    public void RecordLogin(DateTime utcNow) => LastLoginAt = utcNow;

    public void MarkNinVerified(DateTime utcNow) => NinVerifiedAt = utcNow;

    /// <summary>
    /// NDPA erasure (story A3): personal columns are overwritten, the row and its id remain so ledger and learning
    /// records keep referential integrity. The phone hash is replaced so the number can register again.
    /// </summary>
    public void Anonymise(DateTime utcNow)
    {
        PhoneCiphertext = string.Empty;
        PhoneHash = "deleted:" + Id.ToString("N");
        Email = null;
        EmailVerifiedAt = null;
        PasswordHash = null;
        DisplayName = "Deleted user";
        DobCiphertext = string.Empty;
        MfaEnabled = false;
        MfaSecretCiphertext = null;
        CareerGoal = null;
        Interests = [];
        Status = UserStatus.Deleted;
        DeletedAt = utcNow;
    }

    public bool IsActive => Status == UserStatus.Active;
}
