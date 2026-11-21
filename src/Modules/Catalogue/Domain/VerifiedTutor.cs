using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Catalogue.Domain;

/// <summary>Local projection of tutors.tutor-verified.v1 so publish checks never cross module boundaries at runtime.</summary>
public sealed class VerifiedTutor : Entity
{
    private VerifiedTutor()
    {
    }

    private VerifiedTutor(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid TutorId { get; private set; }
    public Guid UserId { get; private set; }
    public string Level { get; private set; } = "new";
    public DateTime VerifiedAt { get; private set; }

    public static VerifiedTutor Record(Guid tenantId, Guid tutorId, Guid userId, string level, DateTime utcNow)
        => new(NewId(), tenantId) { TutorId = tutorId, UserId = userId, Level = level, VerifiedAt = utcNow };

    public void UpdateLevel(string level) => Level = level;
}
