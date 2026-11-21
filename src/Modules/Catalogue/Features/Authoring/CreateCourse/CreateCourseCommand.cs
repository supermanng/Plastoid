using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Catalogue.Features.Authoring.CreateCourse;

public sealed record CreateCourseCommand(string Title, string Summary, string Level, string Language, string Category, IReadOnlyList<string> Skills) : ICommand<CreateCourseResponse>;

public sealed record CreateCourseResponse(Guid CourseId, string Slug, string Status);
