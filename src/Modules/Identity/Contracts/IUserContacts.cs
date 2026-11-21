namespace Plastoids.Modules.Identity.Contracts;

/// <summary>Contact details for outbound messages only (Notifications). Phone is E.164, decrypted at read time; never logged by callers.</summary>
public sealed record UserContact(Guid UserId, string DisplayName, string? PhoneE164, string? Email, bool IsActive);

public interface IUserContacts
{
    Task<UserContact?> GetAsync(Guid userId, CancellationToken cancellationToken);
}
