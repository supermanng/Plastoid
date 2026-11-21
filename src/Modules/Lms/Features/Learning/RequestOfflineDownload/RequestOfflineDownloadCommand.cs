using System.Security.Cryptography;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Integrations;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Lms.Domain;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.Modules.Lms.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Lms.Features.Learning.RequestOfflineDownload;

/// <summary>
/// Story E3: an offline package manifest (lesson list + signed media URLs) with a per-package AES-256 key. The key is
/// returned once to the app and stored encrypted; playback requires the account and stops at the enrolment's end.
/// </summary>
public sealed record RequestOfflineDownloadCommand(Guid EnrolmentId) : ICommand<OfflinePackageResponse>;

public sealed record OfflineLesson(Guid LessonId, string Type, string Title, int DurationMinutes, Uri? MediaUrl, JsonElement? Document);

public sealed record OfflinePackageResponse(Guid PackageId, Guid EnrolmentId, string KeyBase64, DateTime ExpiresAt, IReadOnlyList<OfflineLesson> Lessons);

internal sealed class RequestOfflineDownloadValidator : AbstractValidator<RequestOfflineDownloadCommand>
{
    public RequestOfflineDownloadValidator()
    {
        RuleFor(x => x.EnrolmentId).NotEmpty();
    }
}

internal sealed class RequestOfflineDownloadHandler(LmsDbContext db, LmsAccess access, ProgressService progress, IVideoProvider video, IFieldEncryptor encryptor, IClock clock) : ICommandHandler<RequestOfflineDownloadCommand, OfflinePackageResponse>
{
    public async Task<Result<OfflinePackageResponse>> Handle(RequestOfflineDownloadCommand request, CancellationToken cancellationToken)
    {
        var mine = await access.MyEnrolmentAsync(request.EnrolmentId, cancellationToken);
        if (mine.IsFailure)
        {
            return Result.Failure<OfflinePackageResponse>(mine.Error);
        }

        var enrolment = mine.Value;
        var now = clock.UtcNow;
        if (enrolment.ExpiresAt <= now)
        {
            return Error.Forbidden("enrolment_access_ended", "Access to this course has ended.");
        }

        var course = await progress.CourseForAsync(enrolment, cancellationToken);
        if (course.IsFailure)
        {
            return Result.Failure<OfflinePackageResponse>(course.Error);
        }

        var lessons = course.Value.Lessons.Where(l => l.Type is "video" or "doc").Select(l => new OfflineLesson(l.LessonId, l.Type, l.Title, l.DurationMinutes,
            l.Type == "video" && l.PlaybackId is not null ? video.SignedPlaybackUrl(l.PlaybackId, enrolment.ExpiresAt - now, now) : null, l.Type == "doc" ? l.Content : null)).ToList();

        var key = RandomNumberGenerator.GetBytes(32);
        var manifest = JsonSerializer.Serialize(new { enrolmentId = enrolment.Id, learnerId = enrolment.LearnerId, expiresAt = enrolment.ExpiresAt, lessons = lessons.Select(l => new { l.LessonId, l.Type, l.Title }) });
        var package = OfflinePackage.Issue(enrolment.TenantId, enrolment.Id, enrolment.LearnerId, manifest, encryptor.Encrypt(Convert.ToBase64String(key)), lessons.Count, now, enrolment.ExpiresAt);
        db.OfflinePackages.Add(package);
        return new OfflinePackageResponse(package.Id, enrolment.Id, Convert.ToBase64String(key), enrolment.ExpiresAt, lessons);
    }
}

internal static class RequestOfflineDownloadEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{enrolmentId:guid}/offline-package", async (Guid enrolmentId, ISender sender, CancellationToken ct) => (await sender.Send(new RequestOfflineDownloadCommand(enrolmentId), ct)).ToHttpResult(StatusCodes.Status201Created))
            .WithName("RequestOfflineDownload")
            .WithSummary("Offline package: lesson manifest, media URLs valid until the enrolment ends, and a one-time package key.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
