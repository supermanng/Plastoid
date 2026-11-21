namespace Plastoids.Modules.Identity.Contracts;

/// <summary>PhoneHash is the blind index (keyed HMAC), never the number; AgeYears is derived from the encrypted date of birth, which is never exposed.</summary>
public sealed record UserSummary(Guid UserId, string DisplayName, string? Email, bool IsActive, string PhoneHash = "", int? AgeYears = null);

/// <summary>Minimal in-process lookup of users for other modules (certificate names, notifications, eligibility). Never exposes phone or DOB.</summary>
public interface IUserDirectory
{
    Task<UserSummary?> GetAsync(Guid userId, CancellationToken cancellationToken);
}
