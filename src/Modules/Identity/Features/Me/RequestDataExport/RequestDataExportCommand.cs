using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Identity.Features.Me.RequestDataExport;

public sealed record RequestDataExportCommand : ICommand<RequestDataExportResponse>;

public sealed record RequestDataExportResponse(Guid ExportId, string Status);
