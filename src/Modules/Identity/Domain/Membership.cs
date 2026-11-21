using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Identity.Domain;

/// <summary>Organisation roles (blueprint §9 sponsor portal / §19).</summary>
public static class OrganisationRoles
{
    public const string Owner = "owner";
    public const string Finance = "finance";
    public const string Reviewer = "reviewer";
    public const string Viewer = "viewer";

    public static readonly IReadOnlyCollection<string> All = [Owner, Finance, Reviewer, Viewer];

    public static bool IsValid(string role) => All.Contains(role, StringComparer.Ordinal);
}

/// <summary>
/// Joins a user to the platform (OrganisationId null → platform role such as learner/tutor/staff in this tenant)
/// or to an organisation (owner/finance/reviewer/viewer).
/// </summary>
[Audited]
public sealed class Membership : Entity
{
    private Membership()
    {
    }

    private Membership(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid UserId { get; private set; }
    public Guid? OrganisationId { get; private set; }
    public string Role { get; private set; } = string.Empty;
    public Guid? GrantedBy { get; private set; }

    public static Membership Platform(Guid tenantId, Guid userId, string role, Guid? grantedBy = null)
        => new(NewId(), tenantId) { UserId = userId, OrganisationId = null, Role = role, GrantedBy = grantedBy };

    public static Membership Organisation(Guid tenantId, Guid userId, Guid organisationId, string role, Guid? grantedBy)
        => new(NewId(), tenantId) { UserId = userId, OrganisationId = organisationId, Role = role, GrantedBy = grantedBy };

    public void ChangeRole(string role, Guid changedBy)
    {
        Role = role;
        GrantedBy = changedBy;
    }

    public bool IsPlatformRole => OrganisationId is null;
}
