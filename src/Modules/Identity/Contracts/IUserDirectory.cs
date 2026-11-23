namespace Plastoids.Modules.Identity.Contracts;

/// <summary>PhoneHash is the blind index (keyed HMAC), never the number; AgeYears is derived from the encrypted date of birth, which is never exposed.</summary>
public sealed record UserSummary(Guid UserId, string DisplayName, string? Email, bool IsActive, string PhoneHash = "", int? AgeYears = null);

/// <summary>Minimal in-process lookup of users for other modules (certificate names, notifications, eligibility). Never exposes phone or DOB.</summary>
public interface IUserDirectory
{
    Task<UserSummary?> GetAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Finds an account by phone (any format; normalised and matched on the blind index) or by email (case-insensitive). Null when neither matches (ADR 0021 seat allocation).</summary>
    Task<UserSummary?> FindByContactAsync(string? phone, string? email, CancellationToken cancellationToken);
}
