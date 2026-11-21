using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Catalogue.Domain;

/// <summary>Staff content-review queue item with the automated check results attached.</summary>
public sealed class CourseReviewTask : Entity
{
    private CourseReviewTask()
    {
    }

    private CourseReviewTask(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid CourseId { get; private set; }
    public string AutomatedChecksJson { get; private set; } = "[]";
    public bool RequiresHuman { get; private set; }
    public string Outcome { get; private set; } = "pending";
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? Note { get; private set; }

    public static CourseReviewTask Open(Guid tenantId, Guid courseId, string automatedChecksJson, bool requiresHuman, DateTime utcNow)
    {
        var task = new CourseReviewTask(NewId(), tenantId) { CourseId = courseId, AutomatedChecksJson = automatedChecksJson, RequiresHuman = requiresHuman };
        if (!requiresHuman)
        {
            task.Outcome = "auto_approved";
            task.ReviewedAt = utcNow;
        }

        return task;
    }

    public void Decide(bool approved, Guid reviewerId, string? note, DateTime utcNow)
    {
        Outcome = approved ? "approved" : "rejected";
        ReviewedBy = reviewerId;
        ReviewedAt = utcNow;
        Note = note?.Trim();
    }
}
