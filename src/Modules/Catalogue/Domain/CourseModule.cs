using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Catalogue.Domain;

/// <summary>Draft authoring structure: ordered modules holding ordered lessons. Published versions carry a JSON snapshot.</summary>
public sealed class CourseModule : Entity
{
    private CourseModule()
    {
    }

    private CourseModule(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid CourseId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int Order { get; private set; }

    public static CourseModule Create(Guid tenantId, Guid courseId, string title, int order)
        => new(NewId(), tenantId) { CourseId = courseId, Title = title.Trim(), Order = order };

    public void Update(string title, int order)
    {
        Title = title.Trim();
        Order = order;
    }
}

public static class LessonTypes
{
    public const string Video = "video";
    public const string Document = "doc";
    public const string Quiz = "quiz";
    public const string Assignment = "assignment";
    public const string Live = "live";
    public static readonly IReadOnlyCollection<string> All = [Video, Document, Quiz, Assignment, Live];
    public static bool IsValid(string type) => All.Contains(type, StringComparer.Ordinal);
}

public sealed class Lesson : Entity
{
    private Lesson()
    {
    }

    private Lesson(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid CourseId { get; private set; }
    public Guid ModuleId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public int Order { get; private set; }
    public int DurationMinutes { get; private set; }
    public bool IsPreview { get; private set; }
    public Guid? MediaAssetId { get; private set; }
    public string? ContentJson { get; private set; }

    public static Lesson Create(Guid tenantId, Guid courseId, Guid moduleId, string type, string title, int order, int durationMinutes, bool isPreview, string? contentJson)
        => new(NewId(), tenantId) { CourseId = courseId, ModuleId = moduleId, Type = type, Title = title.Trim(), Order = order, DurationMinutes = durationMinutes, IsPreview = isPreview, ContentJson = contentJson };

    public void AttachMedia(Guid mediaAssetId) => MediaAssetId = mediaAssetId;
}
