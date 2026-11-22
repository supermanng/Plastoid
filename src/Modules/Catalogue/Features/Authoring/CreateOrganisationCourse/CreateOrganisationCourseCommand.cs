using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Catalogue.Features.Authoring.CreateOrganisationCourse;

/// <summary>ADR 0019: a verified organisation's owner or author starts a draft course owned by the organisation.</summary>
public sealed record CreateOrganisationCourseCommand(Guid OrganisationId, string Title, string Summary, string Level, string Language, string Category, IReadOnlyList<string> Skills) : ICommand<CreateOrganisationCourseResponse>;

public sealed record CreateOrganisationCourseRequest(string Title, string Summary, string Level, string Language, string Category, IReadOnlyList<string> Skills);

public sealed record CreateOrganisationCourseResponse(Guid CourseId, string Slug, string Status);
