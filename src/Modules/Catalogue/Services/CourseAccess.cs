using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Catalogue.Services;

/// <summary>Loads a course the caller owns (tutor authoring endpoints) — ownership enforced here, roles by policy.</summary>
internal sealed class CourseAccess(CatalogueDbContext db, ICurrentUser currentUser)
{
    public async Task<Result<Course>> OwnedAsync(Guid courseId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var course = await db.Courses.SingleOrDefaultAsync(c => c.Id == courseId, cancellationToken);
        if (course is null)
        {
            return Error.NotFound("course_not_found", "No such course.");
        }

        if (course.OwnerUserId != userId && !currentUser.Roles.Contains("staff"))
        {
            return Error.Forbidden("not_course_owner", "You do not own this course.");
        }

        return course;
    }

    public async Task<bool> OwnerIsVerifiedTutorAsync(Course course, CancellationToken cancellationToken)
        => course.OwnerType != CourseOwnerType.Tutor || await db.VerifiedTutors.AnyAsync(t => t.UserId == course.OwnerUserId, cancellationToken);
}
