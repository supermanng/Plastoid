using Plastoids.SharedKernel.Time;

namespace Plastoids.Infrastructure.Time;

internal sealed class SystemClock : IClock
{
#pragma warning disable RS0030 // The one allowed use of DateTime.UtcNow.
    public DateTime UtcNow => DateTime.UtcNow;
#pragma warning restore RS0030
}
