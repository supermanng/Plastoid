using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Catalogue.Features.Publishing.ReviewCourse;

public sealed record ReviewCourseCommand(Guid CourseId, string Decision, string? Note, string IdempotencyKey) : ICommand<ReviewCourseResponse>, IIdempotentCommand;

public sealed record ReviewCourseRequest(string Decision, string? Note);

public sealed record ReviewCourseResponse(Guid CourseId, string Status);
