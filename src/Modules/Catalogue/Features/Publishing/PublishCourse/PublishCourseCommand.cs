using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Catalogue.Features.Publishing.PublishCourse;

public sealed record PublishCourseCommand(Guid CourseId) : ICommand<PublishCourseResponse>;

public sealed record PublishCourseResponse(Guid CourseId, int VersionNo, string Slug, DateTime PublishedAt);
