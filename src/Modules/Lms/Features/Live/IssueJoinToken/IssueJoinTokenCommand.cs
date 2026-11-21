using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Lms.Features.Live.IssueJoinToken;

/// <summary>Story E2: a short-lived token for the video SDK; join in two taps, no install on web.</summary>
public sealed record IssueJoinTokenCommand(Guid LiveSessionId) : ICommand<IssueJoinTokenResponse>;

public sealed record IssueJoinTokenResponse(Guid LiveSessionId, string Provider, string RoomId, string Token, Uri JoinUrl, DateTime ExpiresAt, bool IsHost, bool AudioOnlyFallback);

internal sealed class IssueJoinTokenValidator : AbstractValidator<IssueJoinTokenCommand>
{
    public IssueJoinTokenValidator()
    {
        RuleFor(x => x.LiveSessionId).NotEmpty();
    }
}

internal sealed class IssueJoinTokenHandler(LmsDbContext db, ILiveVideoProvider video, ICurrentUser currentUser, IClock clock) : ICommandHandler<IssueJoinTokenCommand, IssueJoinTokenResponse>
{
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(15);

    public async Task<Result<IssueJoinTokenResponse>> Handle(IssueJoinTokenCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var session = await db.LiveSessions.SingleOrDefaultAsync(s => s.Id == request.LiveSessionId, cancellationToken);
        if (session is null || !session.CanJoin(userId))
        {
            return Error.NotFound("session_not_found", "No live session you can join with that id.");
        }

        var window = session.CheckJoinWindow(clock.UtcNow);
        if (window.IsFailure)
        {
            return Result.Failure<IssueJoinTokenResponse>(window.Error);
        }

        var isHost = session.HostUserId == userId;
        var token = await video.IssueJoinTokenAsync(session.RoomId, userId, isHost, TokenLifetime, clock.UtcNow, cancellationToken);
        if (token.IsFailure)
        {
            return Result.Failure<IssueJoinTokenResponse>(token.Error);
        }

        session.MarkLive();
        return new IssueJoinTokenResponse(session.Id, session.Provider, session.RoomId, token.Value.Token, token.Value.JoinUrl, token.Value.ExpiresAt, isHost, true);
    }
}

internal static class IssueJoinTokenEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{liveSessionId:guid}/join-token", async (Guid liveSessionId, ISender sender, CancellationToken ct) => (await sender.Send(new IssueJoinTokenCommand(liveSessionId), ct)).ToHttpResult())
            .WithName("IssueJoinToken")
            .WithSummary("Participant or host: short-lived video SDK token (room opens 15 minutes before the start).")
            .RequireAuthorization(Policies.Authenticated);
    }
}
