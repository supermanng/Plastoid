using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Catalogue.Features.Publishing.PublishCourse;

internal sealed class PublishCourseHandler(CatalogueDbContext db, CourseAccess access, OutlineSnapshot snapshot, IOutbox outbox, IClock clock) : ICommandHandler<PublishCourseCommand, PublishCourseResponse>
{
    public async Task<Result<PublishCourseResponse>> Handle(PublishCourseCommand request, CancellationToken cancellationToken)
    {
        var owned = await access.OwnedAsync(request.CourseId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<PublishCourseResponse>(owned.Error);
        }

        var course = owned.Value;
        var verified = await access.OwnerIsVerifiedAsync(course, cancellationToken);
        var now = clock.UtcNow;
        var published = course.Publish(verified, now);
        if (published.IsFailure)
        {
            return Result.Failure<PublishCourseResponse>(published.Error);
        }

        var outline = await snapshot.BuildAsync(course.Id, cancellationToken);
        var lessonCount = outline.Sum(m => m.Lessons.Count);
        var minutes = outline.Sum(m => m.Lessons.Sum(l => l.DurationMinutes));
        course.SetEstimatedMinutes(minutes);
        db.Versions.Add(CourseVersion.Snapshot(course.TenantId, course.Id, published.Value, OutlineSnapshot.Serialise(outline), lessonCount, minutes, now));

        var offeringIds = await db.Offerings.AsNoTracking().Where(o => o.CourseId == course.Id && o.Active).Select(o => o.Id).ToListAsync(cancellationToken);
        outbox.Enqueue(new CoursePublished(course.Id, course.Id, published.Value, offeringIds, course.OwnerId, course.Slug, course.OwnerTypeName, course.OrganisationId));
        return new PublishCourseResponse(course.Id, published.Value, course.Slug, now);
    }
}
