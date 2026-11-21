using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Lms.Contracts;
using Plastoids.Modules.Lms.Domain;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Lms.Features.Live.HandleProviderAttendanceWebhook;

/// <summary>Join/leave signals from the video provider → AttendanceEvent rows; each completed span emits lms.attendance-recorded.v1.</summary>
public sealed record HandleProviderAttendanceWebhookCommand(string Provider, string Payload) : ICommand<HandleProviderAttendanceWebhookResponse>;

public sealed record HandleProviderAttendanceWebhookResponse(int Joins, int Leaves, int Ignored);

internal sealed class HandleProviderAttendanceWebhookValidator : AbstractValidator<HandleProviderAttendanceWebhookCommand>
{
    public HandleProviderAttendanceWebhookValidator()
    {
        RuleFor(x => x.Provider).NotEmpty();
        RuleFor(x => x.Payload).NotEmpty().MaximumLength(256_000);
    }
}

internal sealed class HandleProviderAttendanceWebhookHandler(LmsDbContext db, ILiveVideoProvider video, IOutbox outbox) : ICommandHandler<HandleProviderAttendanceWebhookCommand, HandleProviderAttendanceWebhookResponse>
{
    public async Task<Result<HandleProviderAttendanceWebhookResponse>> Handle(HandleProviderAttendanceWebhookCommand request, CancellationToken cancellationToken)
    {
        var parsed = video.ParseAttendanceWebhook(request.Payload);
        if (parsed.IsFailure)
        {
            return Result.Failure<HandleProviderAttendanceWebhookResponse>(parsed.Error);
        }

        int joins = 0, leaves = 0, ignored = 0;
        foreach (var signal in parsed.Value.OrderBy(s => s.At))
        {
            var session = await db.LiveSessions.SingleOrDefaultAsync(s => s.RoomId == signal.RoomId, cancellationToken);
            if (session is null || !session.CanJoin(signal.ParticipantUserId))
            {
                ignored++;
                continue;
            }

            if (signal.Kind == "joined")
            {
                if (await db.Attendance.AnyAsync(a => a.ProviderEventId == signal.EventId, cancellationToken))
                {
                    ignored++;
                    continue;
                }

                db.Attendance.Add(AttendanceEvent.Join(session.TenantId, session.Id, signal.ParticipantUserId, signal.At, signal.EventId));
                session.MarkLive();
                joins++;
            }
            else if (signal.Kind == "left")
            {
                var open = await db.Attendance.Where(a => a.LiveSessionId == session.Id && a.UserId == signal.ParticipantUserId && a.LeftAt == null).OrderBy(a => a.JoinedAt).FirstOrDefaultAsync(cancellationToken)
                    ?? db.Attendance.Local.FirstOrDefault(a => a.LiveSessionId == session.Id && a.UserId == signal.ParticipantUserId && a.LeftAt == null);
                if (open is null)
                {
                    ignored++;
                    continue;
                }

                open.Leave(signal.At);
                // Cohort sessions: the attendee's enrolment in that cohort (Sponsorship and Payouts key on it); 1:1 bookings have none.
                Guid? enrolmentId = session.CohortId is { } cohortId
                    ? await db.Enrolments.AsNoTracking().Where(e => e.CohortId == cohortId && e.LearnerId == open.UserId).Select(e => (Guid?)e.Id).FirstOrDefaultAsync(cancellationToken)
                    : null;
                outbox.Enqueue(new AttendanceRecorded(session.Id, session.Id, enrolmentId, open.UserId, session.BookingId, open.JoinedAt, open.LeftAt!.Value, open.DurationSeconds));
                leaves++;
            }
            else
            {
                ignored++;
            }
        }

        return new HandleProviderAttendanceWebhookResponse(joins, leaves, ignored);
    }
}

internal static class HandleProviderAttendanceWebhookEndpoint
{
    public static void Map(IEndpointRouteBuilder lmsGroup)
    {
        lmsGroup.MapPost("/webhooks/live/{provider}", async (string provider, HttpRequest request, ILiveVideoProvider video, ISender sender, CancellationToken ct) =>
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync(ct);
                var signature = request.Headers["X-Live-Signature"].FirstOrDefault() ?? string.Empty;
                if (!string.Equals(provider, video.Name, StringComparison.OrdinalIgnoreCase) || !video.VerifyWebhookSignature(body, signature))
                {
                    return ResultHttp.Problem(Plastoids.SharedKernel.Results.Error.Unauthorized("webhook_signature_invalid", "Webhook signature is missing or invalid."));
                }

                using (AmbientContext.Enter(Tenants.Marketplace, isSystem: true))
                {
                    return (await sender.Send(new HandleProviderAttendanceWebhookCommand(provider.ToLowerInvariant(), body), ct)).ToHttpResult();
                }
            })
            .WithName("LiveAttendanceWebhook")
            .WithSummary("Video provider webhook (signature-verified): join/leave signals become attendance events.")
            .AllowAnonymous()
            .DisableRateLimiting(); // HMAC-verified; providers retry in bursts and must not be throttled with ordinary traffic
    }
}
