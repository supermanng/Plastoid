using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Catalogue.Features.Authoring.ListOrganisationCourses;

/// <summary>Every course the organisation owns, newest edit first (ADR 0019).</summary>
public sealed record ListOrganisationCoursesQuery(Guid OrganisationId) : IQuery<IReadOnlyList<CourseSummary>>;
