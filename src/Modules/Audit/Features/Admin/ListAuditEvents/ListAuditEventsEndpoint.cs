using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Audit.Features.Admin.ListAuditEvents;

internal static class ListAuditEventsEndpoint
{
    public static void Map(IEndpointRouteBuilder adminGroup)
    {
        adminGroup.MapGet("/audit-events", async (string? cursor, int? limit, string? entity, Guid? entityId, Guid? actorId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListAuditEventsQuery(cursor, limit, entity, entityId, actorId), ct)).ToHttpResult())
            .WithName("ListAuditEvents")
            .WithSummary("Staff: immutable audit log, newest first, cursor-paginated. Filters: entity, entityId, actorId.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
