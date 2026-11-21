namespace Plastoids.Modules.Identity.Contracts;

/// <summary>
/// In-process contract for other modules that need to grant a platform role (e.g. Tutors grants "tutor" on application).
/// The implementation enlists the Identity context in the caller's unit of work so the grant commits with the caller's change.
/// </summary>
public interface IPlatformRoles
{
    Task GrantAsync(Guid userId, string role, Guid? grantedBy, CancellationToken cancellationToken);
    Task<bool> HasAsync(Guid userId, string role, CancellationToken cancellationToken);
}
