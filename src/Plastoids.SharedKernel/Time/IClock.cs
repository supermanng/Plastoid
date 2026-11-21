namespace Plastoids.SharedKernel.Time;

/// <summary>The only source of "now". DateTime.Now/UtcNow are banned by the analyzer.</summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
