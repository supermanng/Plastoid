using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Catalogue.Features.Authoring.UploadLessonVideo;

internal static class UploadLessonVideoEndpoint
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static void Map(IEndpointRouteBuilder catalogue)
    {
        catalogue.MapPost("/lessons/{lessonId:guid}/video", async (Guid lessonId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new UploadLessonVideoCommand(lessonId), ct)).ToHttpResult(StatusCodes.Status201Created))
            .WithName("UploadLessonVideo")
            .WithSummary("Owner: get a direct-upload URL for a video lesson; the provider webhook marks it ready after transcoding.")
            .RequireAuthorization(Policies.Tutor);

        // Provider → Plastoids. Signature header: X-Video-Signature (HMAC-SHA256 of the raw body for the fake provider).
        catalogue.MapPost("/webhooks/video/{provider}", async (string provider, HttpRequest request, IVideoProvider video, ISender sender, CancellationToken ct) =>
            {
                using var reader = new StreamReader(request.Body);
                var body = await reader.ReadToEndAsync(ct);
                var signature = request.Headers["X-Video-Signature"].FirstOrDefault() ?? string.Empty;
                if (!string.Equals(provider, video.Name, StringComparison.OrdinalIgnoreCase) || !video.VerifyWebhookSignature(body, signature))
                {
                    return ResultHttp.Problem(Plastoids.SharedKernel.Results.Error.Unauthorized("webhook_signature_invalid", "Webhook signature is missing or invalid."));
                }

                var payload = JsonSerializer.Deserialize<VideoWebhookPayload>(body, Json);
                if (payload is null)
                {
                    return ResultHttp.Problem(Plastoids.SharedKernel.Results.Error.Validation("webhook_payload_invalid", "Body must be JSON."));
                }

                return (await sender.Send(new VideoWebhookCommand(provider.ToLowerInvariant(), payload.UploadId, payload.Status, payload.PlaybackId, payload.DurationSeconds, payload.Renditions, payload.Error), ct)).ToHttpResult();
            })
            .WithName("VideoWebhook")
            .WithSummary("Video provider webhook (signature-verified, idempotent on uploadId).")
            .AllowAnonymous()
            .DisableRateLimiting(); // HMAC-verified; providers retry in bursts and must not be throttled with ordinary traffic
    }
}
