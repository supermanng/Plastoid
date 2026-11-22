using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Behaviours;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Catalogue.Features.Publishing.SubmitForReview;

internal sealed class SubmitForReviewHandler(CatalogueDbContext db, CourseAccess access, AutomatedChecks checks, IClock clock) : ICommandHandler<SubmitForReviewCommand, SubmitForReviewResponse>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<Result<SubmitForReviewResponse>> Handle(SubmitForReviewCommand request, CancellationToken cancellationToken)
    {
        var owned = await access.OwnedAsync(request.CourseId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<SubmitForReviewResponse>(owned.Error);
        }

        var course = owned.Value;
        if (!await access.OwnerIsVerifiedAsync(course, cancellationToken))
        {
            return course.OwnerNotVerified();
        }

        var results = await checks.RunAsync(course, cancellationToken);
        if (results.Any(c => !c.Passed))
        {
            return new ValidationError(results.Where(c => !c.Passed).Select(c => new FieldError(c.Code, c.Code, c.Message)).ToList());
        }

        var submitted = course.Submit(clock.UtcNow);
        if (submitted.IsFailure)
        {
            return Result.Failure<SubmitForReviewResponse>(submitted.Error);
        }

        // First three courses by this owner need a human; afterwards one in five is sampled.
        var reviewedBefore = await db.ReviewTasks.AsNoTracking()
            .Join(db.Courses.AsNoTracking().Where(c => c.OwnerUserId == course.OwnerUserId && c.Id != course.Id), t => t.CourseId, c => c.Id, (t, _) => t)
            .CountAsync(t => t.Outcome != "pending", cancellationToken);
        var requiresHuman = Course.RequiresHumanReview(reviewedBefore, course.Id);
        db.ReviewTasks.Add(CourseReviewTask.Open(course.TenantId, course.Id, JsonSerializer.Serialize(results, Json), requiresHuman, clock.UtcNow));
        if (!requiresHuman)
        {
            course.RecordReview(true, "Automated checks passed; sampled out of human review.");
        }

        return new SubmitForReviewResponse(course.Id, course.Status.ToString().ToLowerInvariant(), requiresHuman, results);
    }
}
