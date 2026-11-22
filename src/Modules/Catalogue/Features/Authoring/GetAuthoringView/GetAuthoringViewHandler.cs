using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Catalogue.Features.Authoring.GetAuthoringView;

internal sealed class GetAuthoringViewHandler(CatalogueDbContext db, CourseAccess access) : IQueryHandler<GetAuthoringViewQuery, AuthoringView>
{
    public async Task<Result<AuthoringView>> Handle(GetAuthoringViewQuery request, CancellationToken cancellationToken)
    {
        var owned = await access.OwnedAsync(request.CourseId, cancellationToken);
        if (owned.IsFailure)
        {
            return Result.Failure<AuthoringView>(owned.Error);
        }

        var course = owned.Value;
        var modules = await db.Modules.AsNoTracking().Where(m => m.CourseId == course.Id).OrderBy(m => m.Order).ToListAsync(cancellationToken);
        var lessons = await db.Lessons.AsNoTracking().Where(l => l.CourseId == course.Id).OrderBy(l => l.Order).ToListAsync(cancellationToken);
        var assetIds = lessons.Where(l => l.MediaAssetId is not null).Select(l => l.MediaAssetId!.Value).ToList();
        var assets = await db.MediaAssets.AsNoTracking().Where(a => assetIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        var offerings = await db.Offerings.AsNoTracking().Where(o => o.CourseId == course.Id).OrderBy(o => o.CreatedAt).ToListAsync(cancellationToken);

        var moduleViews = modules.Select(m => new AuthoringModule(m.Id, m.Title, m.Order, lessons.Where(l => l.ModuleId == m.Id).Select(l => new AuthoringLesson(
            l.Id, l.Type, l.Title, l.Order, l.DurationMinutes, l.IsPreview, l.MediaAssetId,
            l.MediaAssetId is { } assetId && assets.TryGetValue(assetId, out var asset) && asset.Status == MediaStatus.Ready ? asset.PlaybackId : null,
            l.ContentJson)).ToList())).ToList();
        var offeringViews = offerings.Select(o => new AuthoringOffering(o.Id, o.Format, o.Price, o.FundingTypes, o.Capacity, o.StartAt, o.DurationWeeks, o.Active, o.TutorUserId, o.EngagementId)).ToList();

        return new AuthoringView(course.Id, course.Slug, course.Title, course.Summary, course.Level, course.Language, course.Category, course.Skills, course.OwnerTypeName, course.OrganisationId, course.OwnerUserId,
            course.Status.ToString().ToLowerInvariant(), course.ReviewNote, course.CurrentVersionNo, course.PublishedAt, course.SubmittedAt, course.UpdatedAt, moduleViews, offeringViews);
    }
}
