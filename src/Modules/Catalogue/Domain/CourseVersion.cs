using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Catalogue.Domain;

/// <summary>Immutable snapshot of the course outline at publish time (blueprint §16: CourseVersion.outline_json).</summary>
public sealed class CourseVersion : Entity
{
    private CourseVersion()
    {
    }

    private CourseVersion(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid CourseId { get; private set; }
    public int VersionNo { get; private set; }
    public string OutlineJson { get; private set; } = string.Empty;
    public DateTime PublishedAt { get; private set; }
    public int LessonCount { get; private set; }
    public int EstimatedMinutes { get; private set; }

    public static CourseVersion Snapshot(Guid tenantId, Guid courseId, int versionNo, string outlineJson, int lessonCount, int estimatedMinutes, DateTime utcNow)
        => new(NewId(), tenantId) { CourseId = courseId, VersionNo = versionNo, OutlineJson = outlineJson, LessonCount = lessonCount, EstimatedMinutes = estimatedMinutes, PublishedAt = utcNow };
}
