using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Catalogue.Features.Authoring.AddLesson;

internal sealed class AddLessonHandler(CatalogueDbContext db, CourseAccess access) : ICommandHandler<AddLessonCommand, AddLessonResponse>
{
    public async Task<Result<AddLessonResponse>> Handle(AddLessonCommand request, CancellationToken cancellationToken)
    {
        var owned = await access.OwnedAsync(request.CourseId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<AddLessonResponse>(owned.Error);
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

        var lesson = Lesson.Create(course.TenantId, course.Id, request.ModuleId, request.Type, request.Title, request.Order, request.DurationMinutes, request.IsPreview, request.ContentJson);
        db.Lessons.Add(lesson);
        return new AddLessonResponse(lesson.Id, lesson.ModuleId, lesson.Type);
    }
}
