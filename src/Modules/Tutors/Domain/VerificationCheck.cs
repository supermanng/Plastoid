using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Tutors.Domain;

public static class CheckTypes
{
    public const string Identity = "identity";
    public const string Qualifications = "qualifications";
    public const string Subject = "subject";
}

public enum CheckResult
{
    Pending = 0,
    Passed = 1,
    Failed = 2,
}

/// <summary>Audit record of one verification step (provider reference, result, who checked). Raw NIN/BVN are never stored.</summary>
public sealed class VerificationCheck : Entity
{
    private VerificationCheck()
    {
    }

    private VerificationCheck(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid TutorProfileId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Provider { get; private set; } = string.Empty;
    public string Reference { get; private set; } = string.Empty;
    public CheckResult Result { get; private set; }
    public string? Detail { get; private set; }
    public Guid? CheckedBy { get; private set; }
    public DateTime CheckedAt { get; private set; }

    public static VerificationCheck Record(Guid tenantId, Guid tutorProfileId, string type, string provider, string reference, CheckResult result, string? detail, Guid? checkedBy, DateTime utcNow)
        => new(NewId(), tenantId)
        {
            TutorProfileId = tutorProfileId,
            Type = type,
            Provider = provider,
            Reference = reference,
            Result = result,
            Detail = detail,
            CheckedBy = checkedBy,
            CheckedAt = utcNow,
        };
}
