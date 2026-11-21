namespace Plastoids.Modules.Identity.Contracts;

public sealed record UserSummary(Guid UserId, string DisplayName, string? Email, bool IsActive);

/// <summary>Minimal in-process lookup of users for other modules (certificate names, notifications). Never exposes phone or DOB.</summary>
public interface IUserDirectory
{
    Task<UserSummary?> GetAsync(Guid userId, CancellationToken cancellationToken);
}
