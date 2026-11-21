using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Tutors.Domain;

/// <summary>Per-format price per session (blueprint C3). The profile's hourly rate is the default for one-to-one.</summary>
public sealed class PriceRule : Entity
{
    private PriceRule()
    {
    }

    private PriceRule(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid TutorProfileId { get; private set; }
    public string Format { get; private set; } = string.Empty;
    public int DurationMinutes { get; private set; }
    public Money Price { get; private set; }
    public int? MaxLearners { get; private set; }

    public static PriceRule Create(Guid tenantId, Guid tutorProfileId, string format, int durationMinutes, Money price, int? maxLearners)
        => new(NewId(), tenantId) { TutorProfileId = tutorProfileId, Format = format, DurationMinutes = durationMinutes, Price = price, MaxLearners = maxLearners };
}
