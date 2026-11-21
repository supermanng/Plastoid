namespace Plastoids.Modules.Identity.Contracts;

public sealed record OrganisationSummary(Guid OrganisationId, string Name, string Type, bool IsVerified);

/// <summary>In-process lookup of organisations and their memberships for modules that own organisation-scoped aggregates (Sponsorship, Applications).</summary>
public interface IOrganisationDirectory
{
    Task<OrganisationSummary?> GetAsync(Guid organisationId, CancellationToken cancellationToken);

    /// <summary>True when the user holds one of <paramref name="roles"/> (any role when null) in the organisation.</summary>
    Task<bool> IsMemberAsync(Guid userId, Guid organisationId, IReadOnlyCollection<string>? roles, CancellationToken cancellationToken);
}
