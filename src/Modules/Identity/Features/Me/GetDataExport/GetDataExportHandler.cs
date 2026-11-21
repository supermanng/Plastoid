using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Storage;
using Plastoids.Modules.Identity.Domain;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Identity.Features.Me.GetDataExport;

internal sealed class GetDataExportHandler(IdentityDbContext db, IBlobStorage storage, ICurrentUser currentUser) : IQueryHandler<GetDataExportQuery, DataExportResponse>
{
    public async Task<Result<DataExportResponse>> Handle(GetDataExportQuery request, CancellationToken cancellationToken)
    {
        var export = await db.DataExportRequests.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == request.ExportId && r.UserId == currentUser.UserId, cancellationToken);
        if (export is null)
        {
            return Error.NotFound("export_not_found", "No such export request.");
        }

        Uri? json = null;
        Uri? pdf = null;
        if (export.Status == DataExportStatus.Completed)
        {
            json = await storage.GetPresignedDownloadUrlAsync(export.JsonBlobKey!, TimeSpan.FromHours(1), cancellationToken);
            pdf = await storage.GetPresignedDownloadUrlAsync(export.PdfBlobKey!, TimeSpan.FromHours(1), cancellationToken);
        }

        return new DataExportResponse(export.Id, export.Status.ToString().ToLowerInvariant(), export.RequestedAt, export.CompletedAt, json, pdf);
    }
}
