using Microsoft.Extensions.Configuration;

namespace Plastoids.Infrastructure.Secrets;

/// <summary>Key material and secrets. Configuration-backed locally; Key Vault adapter in production.</summary>
public interface ISecretProvider
{
    string GetRequired(string name);
    string? Get(string name);
}

internal sealed class ConfigurationSecretProvider(IConfiguration configuration) : ISecretProvider
{
    public string? Get(string name) => configuration[$"Secrets:{name}"];

    public string GetRequired(string name)
        => Get(name) ?? throw new InvalidOperationException($"Secret '{name}' is not configured (Secrets:{name}).");
}
