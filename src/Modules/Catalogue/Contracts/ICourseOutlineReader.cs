using Plastoids.SharedKernel.Domain;
using System.Text.Json;

namespace Plastoids.Modules.Catalogue.Contracts;

public sealed record PublishedLesson(Guid LessonId, Guid ModuleId, string Type, string Title, int Order, int DurationMinutes, bool IsPreview, string? PlaybackId, JsonElement? Content);

public sealed record PublishedOffering(Guid OfferingId, string Format, int? DurationWeeks, int? Capacity, DateTime? StartAt, Money Price, bool AcceptsSponsorship);

public sealed record PublishedCourse(Guid CourseId, string Slug, string Title, Guid OwnerUserId, int VersionNo, IReadOnlyList<PublishedLesson> Lessons, IReadOnlyList<PublishedOffering> Offerings);

/// <summary>In-process read of a published course version for Lms and Certificates (the outline snapshot is immutable per version).</summary>
public interface ICourseOutlineReader
{
    Task<PublishedCourse?> GetPublishedAsync(Guid courseId, int? versionNo, CancellationToken cancellationToken);
    Task<Guid?> FindCourseForOfferingAsync(Guid offeringId, CancellationToken cancellationToken);
}
