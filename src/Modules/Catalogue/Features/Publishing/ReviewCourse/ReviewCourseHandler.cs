using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Catalogue.Features.Publishing.ReviewCourse;

internal sealed class ReviewCourseHandler(CatalogueDbContext db, ICurrentUser currentUser, IClock clock) : ICommandHandler<ReviewCourseCommand, ReviewCourseResponse>
{
    public async Task<Result<ReviewCourseResponse>> Handle(ReviewCourseCommand request, CancellationToken cancellationToken)
    {
        var course = await db.Courses.SingleOrDefaultAsync(c => c.Id == request.CourseId, cancellationToken);
        if (course is null)
        {
            return Error.NotFound("course_not_found", "No such course.");
        }

        var reviewerId = currentUser.UserId ?? Guid.Empty;
        if (course.OwnerUserId == reviewerId)
        {
            return Error.Forbidden("self_review", "You cannot review your own course.");
        }

        var approved = request.Decision == "approved";
        var decided = course.RecordReview(approved, request.Note);
        if (decided.IsFailure)
        {
            return Result.Failure<ReviewCourseResponse>(decided.Error);
        }

        var task = await db.ReviewTasks.Where(t => t.CourseId == course.Id && t.Outcome == "pending").OrderByDescending(t => t.CreatedAt).FirstOrDefaultAsync(cancellationToken);
        task?.Decide(approved, reviewerId, request.Note, clock.UtcNow);
        return new ReviewCourseResponse(course.Id, course.Status.ToString().ToLowerInvariant());
    }
}
