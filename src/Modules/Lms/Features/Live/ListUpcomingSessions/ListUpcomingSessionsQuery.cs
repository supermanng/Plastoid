using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Lms.Domain;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Lms.Features.Live.ListUpcomingSessions;

public sealed record ListUpcomingSessionsQuery(int? Days) : IQuery<IReadOnlyList<UpcomingSession>>;

public sealed record UpcomingSession(Guid LiveSessionId, string Title, string Source, Guid? BookingId, Guid? CohortId, DateTime StartsAt, DateTime EndsAt, string Status, bool IsHost, bool JoinOpen);

internal sealed class ListUpcomingSessionsHandler(LmsDbContext db, ICurrentUser currentUser, IClock clock) : IQueryHandler<ListUpcomingSessionsQuery, IReadOnlyList<UpcomingSession>>
{
    public async Task<Result<IReadOnlyList<UpcomingSession>>> Handle(ListUpcomingSessionsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? Guid.Empty;
        var now = clock.UtcNow;
        var until = now.AddDays(Math.Clamp(request.Days ?? 14, 1, 60));
        // Participants are a JSON array; filter in memory over the upcoming window (small per user).
        var sessions = await db.LiveSessions.AsNoTracking()
            .Where(s => s.Status != LiveSessionStatus.Cancelled && s.EndsAt >= now && s.StartsAt <= until)
            .OrderBy(s => s.StartsAt)
            .Take(500)
            .ToListAsync(cancellationToken);
        return sessions.Where(s => s.CanJoin(userId))
            .Select(s => new UpcomingSession(s.Id, s.Title, s.Source, s.BookingId, s.CohortId, s.StartsAt, s.EndsAt, s.Status.ToString().ToLowerInvariant(), s.HostUserId == userId, s.CheckJoinWindow(now).IsSuccess))
            .ToList();
    }
}

internal static class ListUpcomingSessionsEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/upcoming", async (int? days, ISender sender, CancellationToken ct) => (await sender.Send(new ListUpcomingSessionsQuery(days), ct)).ToHttpResult())
            .WithName("ListUpcomingSessions")
            .WithSummary("My live sessions (as host or participant) for the next N days (default 14).")
            .RequireAuthorization(Policies.Authenticated);
    }
}
