using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Tutors.Domain;

public enum CredentialStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
}

public static class CredentialTypes
{
    public const string Degree = "degree";
    public const string Certificate = "certificate";
    public const string WorkHistory = "work_history";
    public const string LinkedIn = "linkedin";
    public static readonly IReadOnlyCollection<string> All = [Degree, Certificate, WorkHistory, LinkedIn];
    public static bool IsValid(string type) => All.Contains(type, StringComparer.Ordinal);
}

/// <summary>Blueprint §13 stage 2: a degree/certificate/work-history item reviewed by a Plastoids reviewer.</summary>
[Audited]
public sealed class Credential : Entity
{
    private Credential()
    {
    }

    private Credential(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid TutorProfileId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Issuer { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string EvidenceBlobKey { get; private set; } = string.Empty;
    public CredentialStatus Status { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? ReviewNote { get; private set; }

    public static Credential Submit(Guid tenantId, Guid tutorProfileId, string type, string issuer, string title, string evidenceBlobKey)
        => new(NewId(), tenantId)
        {
            TutorProfileId = tutorProfileId,
            Type = type,
            Issuer = issuer.Trim(),
            Title = title.Trim(),
            EvidenceBlobKey = evidenceBlobKey,
            Status = CredentialStatus.Pending,
        };

    public Result Review(bool approved, Guid reviewerId, string? note, DateTime utcNow)
    {
        if (Status != CredentialStatus.Pending)
        {
            return Error.Conflict("credential_already_reviewed", "This credential was already reviewed.");
        }

        Status = approved ? CredentialStatus.Approved : CredentialStatus.Rejected;
        ReviewedBy = reviewerId;
        ReviewedAt = utcNow;
        ReviewNote = note?.Trim();
        return Result.Success();
    }
}
