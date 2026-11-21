using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Catalogue.Features.Discovery.CompareCourses;

/// <summary>Story B2: side by side on price, duration, format, rating, outcomes (up to 3).</summary>
public sealed record CompareCoursesQuery(IReadOnlyList<Guid> Ids) : IQuery<CompareCoursesResponse>;

public sealed record CompareRow(Guid CourseId, string Slug, string Title, string Level, Money? FromPrice, IReadOnlyList<string> Formats, bool SponsoredSeats, int EstimatedMinutes, int LessonCount, decimal RatingAverage, int RatingCount, IReadOnlyList<string> Skills);

public sealed record CompareCoursesResponse(IReadOnlyList<CompareRow> Courses);
