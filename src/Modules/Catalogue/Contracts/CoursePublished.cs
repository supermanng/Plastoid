using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Catalogue.Contracts;

/// <summary>catalogue.course-published.v1 (build prompt §2.8). Lms copies the outline; Notifications tells followers.</summary>
[IntegrationEvent("catalogue", "course-published", 1)]
public sealed record CoursePublished(Guid AggregateId, Guid CourseId, int VersionNo, IReadOnlyList<Guid> OfferingIds, Guid OwnerId, string Slug, string OwnerType = CourseOwnerTypes.Tutor, Guid? OrganisationId = null) : IIntegrationEvent;
