using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Identity.Domain;

/// <summary>Tenant row. Tenant zero is the Plastoids marketplace; institution tenants arrive in Phase 3.</summary>
public sealed class Tenant : Entity
{
    private Tenant()
    {
    }

    private Tenant(Guid id) : base(id, id)
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;

    public static Tenant Create(Guid id, string name, string slug) => new(id) { Name = name, Slug = slug };
}
