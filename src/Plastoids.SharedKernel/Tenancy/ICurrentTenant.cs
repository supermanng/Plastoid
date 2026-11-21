namespace Plastoids.SharedKernel.Tenancy;

/// <summary>
/// The tenant the current request/job runs under. The marketplace is tenant zero (<see cref="Tenants.Marketplace"/>).
/// MySQL has no row-level security, so this is enforced by query filters and a write guard in the persistence layer.
/// </summary>
public interface ICurrentTenant
{
    Guid TenantId { get; }

    /// <summary>True for back-office/system contexts that may read across tenants. Every such read is audited.</summary>
    bool IsSystemContext { get; }
}

public static class Tenants
{
    /// <summary>Tenant zero: the Plastoids marketplace. Fixed, seeded, never changes.</summary>
    public static readonly Guid Marketplace = new("00000000-0000-7000-8000-000000000001");
}
