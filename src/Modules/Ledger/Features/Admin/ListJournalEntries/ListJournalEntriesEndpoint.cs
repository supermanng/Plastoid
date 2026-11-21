using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Ledger.Features.Admin.ListJournalEntries;

internal static class ListJournalEntriesEndpoint
{
    public static void Map(IEndpointRouteBuilder adminGroup)
    {
        adminGroup.MapGet("/entries", async (string? cursor, int? limit, string? kind, string? accountCode, Guid? ownerId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ListJournalEntriesQuery(cursor, limit, kind, accountCode, ownerId), ct)).ToHttpResult())
            .WithName("ListJournalEntries")
            .WithSummary("Staff: journal entries with postings, newest first; filter by kind, accountCode, ownerId.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
