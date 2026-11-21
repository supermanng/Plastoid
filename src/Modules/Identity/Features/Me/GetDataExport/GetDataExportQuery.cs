using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Identity.Features.Me.GetDataExport;

public sealed record GetDataExportQuery(Guid ExportId) : IQuery<DataExportResponse>;

public sealed record DataExportResponse(Guid ExportId, string Status, DateTime RequestedAt, DateTime? CompletedAt, Uri? JsonUrl, Uri? PdfUrl);
