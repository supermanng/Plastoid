using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Catalogue.Features.Authoring.AddLesson;

public sealed record AddLessonCommand(Guid CourseId, Guid ModuleId, string Type, string Title, int Order, int DurationMinutes, bool IsPreview, string? ContentJson) : ICommand<AddLessonResponse>;

public sealed record AddLessonRequest(string Type, string Title, int Order, int DurationMinutes, bool IsPreview, string? ContentJson);

public sealed record AddLessonResponse(Guid LessonId, Guid ModuleId, string Type);
