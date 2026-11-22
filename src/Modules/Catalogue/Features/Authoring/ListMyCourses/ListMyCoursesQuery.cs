using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Catalogue.Features.Authoring.ListMyCourses;

/// <summary>Courses the signed-in tutor created (OwnerUserId), newest edit first.</summary>
public sealed record ListMyCoursesQuery : IQuery<IReadOnlyList<CourseSummary>>;
