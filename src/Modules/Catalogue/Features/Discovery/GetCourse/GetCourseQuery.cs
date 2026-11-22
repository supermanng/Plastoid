using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Catalogue.Features.Discovery.GetCourse;

/// <summary>By slug. Issues a 30-minute price quote per offering so the shown price is honoured at checkout (blueprint §9.2).</summary>
public sealed record GetCourseQuery(string Slug) : IQuery<CourseDetail>;

public sealed record OfferingView(Guid OfferingId, string Format, Money Price, IReadOnlyList<string> FundingTypes, int? Capacity, DateTime? StartAt, int? DurationWeeks, string QuoteId, DateTime PriceValidUntil);

public sealed record LessonView(Guid Id, string Type, string Title, int Order, int DurationMinutes, bool IsPreview, Uri? PreviewPlaybackUrl);

public sealed record ModuleView(Guid Id, string Title, int Order, IReadOnlyList<LessonView> Lessons);

public sealed record ReviewView(int Rating, string Body, DateTime CreatedAt);

/// <summary>The organisation that owns and sells the course (ADR 0019); absent on tutor courses.</summary>
public sealed record ProviderView(Guid OrganisationId, string Name);

public sealed record CourseDetail(
    Guid CourseId,
    string Slug,
    string Title,
    string Summary,
    string Level,
    string Language,
    string Category,
    IReadOnlyList<string> Skills,
    Guid OwnerId,
    string OwnerType,
    ProviderView? Provider,
    int VersionNo,
    DateTime? PublishedAt,
    int LessonCount,
    int EstimatedMinutes,
    decimal RatingAverage,
    int RatingCount,
    IReadOnlyList<ModuleView> Outline,
    IReadOnlyList<OfferingView> Offerings,
    IReadOnlyList<ReviewView> RecentReviews);
