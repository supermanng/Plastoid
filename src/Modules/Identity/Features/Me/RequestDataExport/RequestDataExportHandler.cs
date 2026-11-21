using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Identity.Domain;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Identity.Features.Me.RequestDataExport;

internal sealed class RequestDataExportHandler(IdentityDbContext db, ICurrentUser currentUser, IClock clock) : ICommandHandler<RequestDataExportCommand, RequestDataExportResponse>
{
    public async Task<Result<RequestDataExportResponse>> Handle(RequestDataExportCommand request, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == currentUser.UserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var pending = await db.DataExportRequests.SingleOrDefaultAsync(r => r.UserId == user.Id && r.Status == DataExportStatus.Pending, cancellationToken);
        if (pending is not null)
        {
            return new RequestDataExportResponse(pending.Id, pending.Status.ToString().ToLowerInvariant());
        }

        var export = DataExportRequest.Create(user.TenantId, user.Id, clock.UtcNow);
        db.DataExportRequests.Add(export);
        return new RequestDataExportResponse(export.Id, export.Status.ToString().ToLowerInvariant());
    }
}
