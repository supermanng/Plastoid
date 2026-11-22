using Plastoids.SharedKernel.Domain;
using System.Text.Json;

namespace Plastoids.Modules.Catalogue.Contracts;

public sealed record PublishedLesson(Guid LessonId, Guid ModuleId, string Type, string Title, int Order, int DurationMinutes, bool IsPreview, string? PlaybackId, JsonElement? Content);

/// <summary>TutorUserId / EngagementId: the engaged tutor who teaches this offering of an organisation course (ADR 0019); null on tutor-owned courses.</summary>
public sealed record PublishedOffering(Guid OfferingId, string Format, int? DurationWeeks, int? Capacity, DateTime? StartAt, Money Price, bool AcceptsSponsorship, Guid? TutorUserId = null, Guid? EngagementId = null);

/// <summary>OwnerType is "tutor", "organisation" or "plastoids" (ADR 0019); OrganisationId is set for organisation-owned courses.</summary>
public sealed record PublishedCourse(Guid CourseId, string Slug, string Title, Guid OwnerUserId, int VersionNo, IReadOnlyList<PublishedLesson> Lessons, IReadOnlyList<PublishedOffering> Offerings, string OwnerType = CourseOwnerTypes.Tutor, Guid? OrganisationId = null);

/// <summary>Course owner kinds as other modules see them (lower-case names of Catalogue's CourseOwnerType).</summary>
public static class CourseOwnerTypes
{
    public const string Tutor = "tutor";
    public const string Tenant = "tenant";
    public const string Plastoids = "plastoids";
    public const string Organisation = "organisation";
}

/// <summary>In-process read of a published course version for Lms and Certificates (the outline snapshot is immutable per version).</summary>
public interface ICourseOutlineReader
{
    Task<PublishedCourse?> GetPublishedAsync(Guid courseId, int? versionNo, CancellationToken cancellationToken);
    Task<Guid?> FindCourseForOfferingAsync(Guid offeringId, CancellationToken cancellationToken);
}
