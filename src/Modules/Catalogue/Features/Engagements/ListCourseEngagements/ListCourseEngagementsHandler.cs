using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Catalogue.Features.Engagements.ListCourseEngagements;

internal sealed class ListCourseEngagementsHandler(CatalogueDbContext db, EngagementViews views) : IQueryHandler<ListCourseEngagementsQuery, IReadOnlyList<EngagementView>>
{
    public async Task<Result<IReadOnlyList<EngagementView>>> Handle(ListCourseEngagementsQuery request, CancellationToken cancellationToken)
    {
        var rows = await db.Engagements.AsNoTracking().Where(e => e.CourseId == request.CourseId && e.OrganisationId == request.OrganisationId).OrderByDescending(e => e.OfferedAt).Take(200).ToListAsync(cancellationToken);
        return Result.Success(await views.ListAsync(rows, cancellationToken));
    }
}
