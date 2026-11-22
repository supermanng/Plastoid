using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Catalogue.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Catalogue.Features.Engagements.ListMyEngagements;

internal sealed class ListMyEngagementsHandler(CatalogueDbContext db, EngagementViews views, ICurrentUser currentUser) : IQueryHandler<ListMyEngagementsQuery, IReadOnlyList<EngagementView>>
{
    public async Task<Result<IReadOnlyList<EngagementView>>> Handle(ListMyEngagementsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var rows = await db.Engagements.AsNoTracking().Where(e => e.TutorUserId == userId).OrderByDescending(e => e.OfferedAt).Take(200).ToListAsync(cancellationToken);
        return Result.Success(await views.ListAsync(rows, cancellationToken));
    }
}
