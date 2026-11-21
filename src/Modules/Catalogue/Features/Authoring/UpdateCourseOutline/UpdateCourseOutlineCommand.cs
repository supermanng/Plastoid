using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Catalogue.Features.Authoring.UpdateCourseOutline;

public sealed record OutlineModuleInput(Guid? Id, string Title, int Order);

/// <summary>Updates course metadata and replaces the module list (modules with an id are renamed/reordered, others created, missing ones deleted with their lessons).</summary>
public sealed record UpdateCourseOutlineCommand(Guid CourseId, string? Title, string? Summary, string? Level, string? Language, string? Category, IReadOnlyList<string>? Skills, IReadOnlyList<OutlineModuleInput> Modules) : ICommand<UpdateCourseOutlineResponse>;

public sealed record UpdateCourseOutlineRequest(string? Title, string? Summary, string? Level, string? Language, string? Category, IReadOnlyList<string>? Skills, IReadOnlyList<OutlineModuleInput> Modules);

public sealed record UpdateCourseOutlineResponse(Guid CourseId, string Status, IReadOnlyList<OutlineModuleInput> Modules);
