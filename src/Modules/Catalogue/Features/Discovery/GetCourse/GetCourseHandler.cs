using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Catalogue.Services;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Catalogue.Features.Discovery.GetCourse;

internal sealed class GetCourseHandler(CatalogueDbContext db, PriceQuotes quotes, IVideoProvider video, IOrganisationDirectory organisations, ICurrentUser currentUser, IClock clock) : IQueryHandler<GetCourseQuery, CourseDetail>
{
    public async Task<Result<CourseDetail>> Handle(GetCourseQuery request, CancellationToken cancellationToken)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(c => c.Slug == request.Slug, cancellationToken);
        var visible = course is not null && course.CurrentVersionNo > 0
            && (course.Status == CourseStatus.Published || (course.Status == CourseStatus.Unpublished && currentUser.UserId is not null));
        if (course is null || !visible)
        {
            // Unpublished courses stay reachable for signed-in (enrolled) learners; Lms enforces access to content.
            return Error.NotFound("course_not_found", "No published course with that slug.");
        }

        var version = await db.Versions.AsNoTracking().SingleAsync(v => v.CourseId == course.Id && v.VersionNo == course.CurrentVersionNo, cancellationToken);
        var outline = OutlineSnapshot.Deserialise(version.OutlineJson);
        var offerings = course.Status == CourseStatus.Published
            ? await db.Offerings.AsNoTracking().Where(o => o.CourseId == course.Id && o.Active).ToListAsync(cancellationToken)
            : [];
        var reviews = await db.Reviews.AsNoTracking().Where(r => r.CourseId == course.Id).OrderByDescending(r => r.CreatedAt).Take(5).ToListAsync(cancellationToken);

        var offeringViews = new List<OfferingView>();
        foreach (var o in offerings)
        {
            var quote = await quotes.IssueAsync(o.Id, o.Price, currentUser.UserId);
            offeringViews.Add(new OfferingView(o.Id, o.Format, o.Price, o.FundingTypes, o.Capacity, o.StartAt, o.DurationWeeks, quote.QuoteId, quote.ValidUntil));
        }

        var modules = outline.Select(m => new ModuleView(m.Id, m.Title, m.Order, m.Lessons.Select(l => new LessonView(
            l.Id, l.Type, l.Title, l.Order, l.DurationMinutes, l.IsPreview,
            l.IsPreview && l.PlaybackId is not null ? video.SignedPlaybackUrl(l.PlaybackId, TimeSpan.FromHours(1), clock.UtcNow) : null)).ToList())).ToList();

        ProviderView? provider = null;
        if (course.IsOrganisationOwned && course.OrganisationId is { } organisationId && await organisations.GetAsync(organisationId, cancellationToken) is { } organisation)
        {
            provider = new ProviderView(organisation.OrganisationId, organisation.Name);
        }

        return new CourseDetail(course.Id, course.Slug, course.Title, course.Summary, course.Level, course.Language, course.Category, course.Skills, course.OwnerId, course.OwnerTypeName, provider,
            version.VersionNo, course.PublishedAt, version.LessonCount, version.EstimatedMinutes, course.RatingAverage, course.RatingCount,
            modules, offeringViews, reviews.Select(r => new ReviewView(r.Rating, r.Body, r.CreatedAt)).ToList());
    }
}
