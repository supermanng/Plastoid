using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Catalogue.Features.Authoring.AddQuiz;

internal sealed class AddQuizHandler(CatalogueDbContext db, CourseAccess access) : ICommandHandler<AddQuizCommand, AddQuizResponse>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<AddQuizResponse>> Handle(AddQuizCommand request, CancellationToken cancellationToken)
    {
        var owned = await access.OwnedAsync(request.CourseId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<AddQuizResponse>(owned.Error);
        }

        var course = owned.Value;
        if (course.Status == CourseStatus.InReview)
        {
            return Error.Conflict("course_in_review", "The course is being reviewed.");
        }

        if (!await db.Modules.AnyAsync(m => m.Id == request.ModuleId && m.CourseId == course.Id, cancellationToken))
        {
            return Error.NotFound("module_not_found", "No such module in this course.");
        }

        var content = JsonSerializer.Serialize(new { passMarkPercent = request.PassMarkPercent, questions = request.Questions }, Json);
        var lesson = Lesson.Create(course.TenantId, course.Id, request.ModuleId, LessonTypes.Quiz, request.Title, request.Order, Math.Max(1, request.Questions.Count), false, content);
        db.Lessons.Add(lesson);
        return new AddQuizResponse(lesson.Id, request.Questions.Count);
    }
}
