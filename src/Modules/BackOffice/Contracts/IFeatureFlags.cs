namespace Plastoids.Modules.BackOffice.Contracts;

/// <summary>Staff-managed feature flags (blueprint §9.20). Flags default to off; modules read them in-process.</summary>
public interface IFeatureFlags
{
    Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken);
}
