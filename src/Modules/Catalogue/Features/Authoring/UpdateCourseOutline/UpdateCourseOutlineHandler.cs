using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Catalogue.Features.Authoring.UpdateCourseOutline;

internal sealed class UpdateCourseOutlineHandler(CatalogueDbContext db, CourseAccess access) : ICommandHandler<UpdateCourseOutlineCommand, UpdateCourseOutlineResponse>
{
    public async Task<Result<UpdateCourseOutlineResponse>> Handle(UpdateCourseOutlineCommand request, CancellationToken cancellationToken)
    {
        var owned = await access.OwnedAsync(request.CourseId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<UpdateCourseOutlineResponse>(owned.Error);
        }

        var course = owned.Value;
        var updated = course.UpdateOutline(request.Title, request.Summary, request.Level, request.Language, request.Category, request.Skills);
        if (updated.IsFailure)
        {
            return Result.Failure<UpdateCourseOutlineResponse>(updated.Error);
        }

        var existing = await db.Modules.Where(m => m.CourseId == course.Id).ToListAsync(cancellationToken);
        var keep = new HashSet<Guid>();
        var result = new List<OutlineModuleInput>();
        foreach (var input in request.Modules.OrderBy(m => m.Order))
        {
            var module = input.Id is { } id ? existing.SingleOrDefault(m => m.Id == id) : null;
            if (module is null)
            {
                module = CourseModule.Create(course.TenantId, course.Id, input.Title, input.Order);
                db.Modules.Add(module);
            }
            else
            {
                module.Update(input.Title, input.Order);
            }

            keep.Add(module.Id);
            result.Add(new OutlineModuleInput(module.Id, module.Title, module.Order));
        }

        var removed = existing.Where(m => !keep.Contains(m.Id)).ToList();
        if (removed.Count > 0)
        {
            var removedIds = removed.Select(m => m.Id).ToList();
            db.Lessons.RemoveRange(await db.Lessons.Where(l => removedIds.Contains(l.ModuleId)).ToListAsync(cancellationToken));
            db.Modules.RemoveRange(removed);
        }

        return new UpdateCourseOutlineResponse(course.Id, course.Status.ToString().ToLowerInvariant(), result);
    }
}
