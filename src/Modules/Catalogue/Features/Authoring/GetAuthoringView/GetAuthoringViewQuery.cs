using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Catalogue.Features.Authoring.GetAuthoringView;

/// <summary>The draft outline (modules, lessons, offerings) as the author sees it, read from the draft tables rather than the published snapshot.</summary>
public sealed record GetAuthoringViewQuery(Guid CourseId) : IQuery<AuthoringView>;

/// <summary>ContentJson holds the quiz definition (passMarkPercent, questions) for quiz lessons; PlaybackId is set once a video lesson is transcoded.</summary>
public sealed record AuthoringLesson(Guid Id, string Type, string Title, int Order, int DurationMinutes, bool IsPreview, Guid? MediaAssetId, string? PlaybackId, string? ContentJson);

public sealed record AuthoringModule(Guid Id, string Title, int Order, IReadOnlyList<AuthoringLesson> Lessons);

/// <summary>TutorUserId / EngagementId name the engaged tutor on an organisation course's offering (ADR 0019).</summary>
public sealed record AuthoringOffering(Guid Id, string Format, Money Price, IReadOnlyList<string> FundingTypes, int? Capacity, DateTime? StartAt, int? DurationWeeks, bool Active, Guid? TutorUserId, Guid? EngagementId);

public sealed record AuthoringView(
    Guid CourseId,
    string Slug,
    string Title,
    string Summary,
    string Level,
    string Language,
    string Category,
    IReadOnlyList<string> Skills,
    string OwnerType,
    Guid? OrganisationId,
    Guid OwnerUserId,
    string Status,
    string? ReviewNote,
    int CurrentVersionNo,
    DateTime? PublishedAt,
    DateTime? SubmittedAt,
    DateTime UpdatedAt,
    IReadOnlyList<AuthoringModule> Modules,
    IReadOnlyList<AuthoringOffering> Offerings);
