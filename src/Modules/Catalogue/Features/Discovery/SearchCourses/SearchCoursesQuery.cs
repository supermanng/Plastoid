using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Catalogue.Features.Discovery.SearchCourses;

/// <summary>Story B1: full-text + combinable filters; zero results come back with suggestions.</summary>
public sealed record SearchCoursesQuery(string? Q, string? Skill, string? Level, string? Funding, string? Format, long? MaxPriceKobo, string? Lang, string? Category, string? Cursor, int? Limit) : IQuery<SearchCoursesResponse>;

/// <summary>ProviderName: the owning organisation's name for organisation courses (ADR 0019); null for tutor courses.</summary>
public sealed record CourseCard(Guid CourseId, string Slug, string Title, string Summary, string Level, string Language, string Category, IReadOnlyList<string> Skills, Money? FromPrice, IReadOnlyList<string> Formats, bool SponsoredSeats, decimal RatingAverage, int RatingCount, int EstimatedMinutes, string? ProviderName = null);

public sealed record SearchSuggestions(IReadOnlyList<string> Skills, IReadOnlyList<string> Categories, IReadOnlyList<CourseCard> Popular);

public sealed record SearchCoursesResponse(IReadOnlyList<CourseCard> Items, string? NextCursor, int Total, SearchSuggestions? Suggestions);
