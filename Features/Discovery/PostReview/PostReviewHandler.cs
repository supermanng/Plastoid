using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Catalogue.Features.Discovery.PostReview;

internal sealed class PostReviewHandler(CatalogueDbContext db, IEnrolmentProgress progress, ICurrentUser currentUser) : ICommandHandler<PostReviewCommand, PostReviewResponse>
{
    public const decimal MinimumProgressPercent = 30m;

    public async Task<Result<PostReviewResponse>> Handle(PostReviewCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } learnerId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var course = await db.Courses.SingleOrDefaultAsync(c => c.Id == request.CourseId && c.Status == CourseStatus.Published, cancellationToken);
        if (course is null)
        {
            return Error.NotFound("course_not_found", "No such published course.");
        }

        var snapshot = await progress.GetAsync(request.EnrolmentId, learnerId, course.Id, cancellationToken);
        if (snapshot is null || !snapshot.IsVerified)
        {
            return Error.Forbidden("review_requires_enrolment", "Only verified enrolments can review this course.");
        }

        if (snapshot.ProgressPercent < MinimumProgressPercent)
        {
            return Error.Validation("review_requires_progress", $"Reviews open after {MinimumProgressPercent:0}% of the course is completed.");
        }

        if (await db.Reviews.AnyAsync(r => r.CourseId == course.Id && r.LearnerId == learnerId, cancellationToken))
        {
            return Error.Conflict("review_exists", "You already reviewed this course.");
        }

        var review = Review.Create(course.TenantId, course.Id, learnerId, request.EnrolmentId, request.Rating, request.Body, snapshot.ProgressPercent);
        db.Reviews.Add(review);
        course.RecordRating(request.Rating);
        return new PostReviewResponse(review.Id, course.RatingAverage, course.RatingCount);
    }
}
