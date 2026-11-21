using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;

namespace Plastoids.Modules.Catalogue.Services;

public sealed record CheckResult(string Code, bool Passed, string Message);

/// <summary>The "automated checks" half of course review (story C2): structure, media readiness, pricing and copy length.</summary>
internal sealed class AutomatedChecks(CatalogueDbContext db)
{
    public async Task<IReadOnlyList<CheckResult>> RunAsync(Course course, CancellationToken cancellationToken)
    {
        var modules = await db.Modules.AsNoTracking().CountAsync(m => m.CourseId == course.Id, cancellationToken);
        var lessons = await db.Lessons.AsNoTracking().Where(l => l.CourseId == course.Id).ToListAsync(cancellationToken);
        var offerings = await db.Offerings.AsNoTracking().CountAsync(o => o.CourseId == course.Id && o.Active, cancellationToken);
        var videoLessonAssetIds = lessons.Where(l => l.Type == LessonTypes.Video).Select(l => l.MediaAssetId).ToList();
        var readyAssets = videoLessonAssetIds.Count == 0 ? 0 : await db.MediaAssets.AsNoTracking().CountAsync(a => videoLessonAssetIds.Contains(a.Id) && a.Status == MediaStatus.Ready, cancellationToken);

        return
        [
            new("has_module", modules > 0, modules > 0 ? $"{modules} module(s)" : "Add at least one module."),
            new("has_lesson", lessons.Count > 0, lessons.Count > 0 ? $"{lessons.Count} lesson(s)" : "Add at least one lesson."),
            new("has_offering", offerings > 0, offerings > 0 ? $"{offerings} active offering(s)" : "Create an offering (price, format) before submitting."),
            new("videos_ready", readyAssets == videoLessonAssetIds.Count(id => id is not null) && videoLessonAssetIds.All(id => id is not null),
                videoLessonAssetIds.Count == 0 ? "No video lessons." : $"{readyAssets}/{videoLessonAssetIds.Count} videos transcoded."),
            new("summary_length", course.Summary.Length >= 80, course.Summary.Length >= 80 ? "Summary ok" : "Summary must be at least 80 characters."),
            new("has_skills", course.Skills.Count > 0, course.Skills.Count > 0 ? "Skills tagged" : "Tag at least one skill for search."),
        ];
    }
}
