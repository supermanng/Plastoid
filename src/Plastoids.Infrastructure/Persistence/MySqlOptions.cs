namespace Plastoids.Infrastructure.Persistence;

public sealed class MySqlOptions
{
    public const string SectionName = "MySql";

    /// <summary>Server version string for Pomelo, e.g. "8.4.0-mysql". Avoids a round-trip at startup.</summary>
    public string ServerVersion { get; set; } = "8.4.0-mysql";
}
