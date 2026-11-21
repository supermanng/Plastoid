using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Catalogue.Features.Authoring.CreateCourse;

internal sealed class CreateCourseHandler(CatalogueDbContext db, ICurrentUser currentUser, ICurrentTenant tenant) : ICommandHandler<CreateCourseCommand, CreateCourseResponse>
{
    public Task<Result<CreateCourseResponse>> Handle(CreateCourseCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Task.FromResult(Result.Failure<CreateCourseResponse>(Error.Unauthorized("not_signed_in", "Sign in first.")));
        }

        var ownerType = currentUser.Roles.Contains("staff") ? CourseOwnerType.Plastoids : CourseOwnerType.Tutor;
        var suffix = Guid.CreateVersion7().ToString("N")[^6..];
        var course = Course.Create(tenant.TenantId, ownerType, userId, userId, request.Title, request.Summary, request.Level, request.Language, request.Category, request.Skills, suffix);
        db.Courses.Add(course);
        return Task.FromResult(Result.Success(new CreateCourseResponse(course.Id, course.Slug, course.Status.ToString().ToLowerInvariant())));
    }
}
