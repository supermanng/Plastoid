using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Catalogue.Features.Engagements.ListCourseEngagements;

/// <summary>Every engagement (any status) on one of the organisation's courses, newest offer first.</summary>
public sealed record ListCourseEngagementsQuery(Guid OrganisationId, Guid CourseId) : IQuery<IReadOnlyList<EngagementView>>;
