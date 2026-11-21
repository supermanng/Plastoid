using Plastoids.Infrastructure.Storage;
using Plastoids.Infrastructure.Time;
using Plastoids.Modules.Tutors.Domain;
using Plastoids.Modules.Tutors.Persistence;
using Plastoids.Modules.Tutors.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Tutors.Features.Onboarding.UploadCredential;

internal sealed class UploadCredentialHandler(TutorsDbContext db, TutorAccess access, IBlobStorage storage, IClock clock)
    : ICommandHandler<UploadCredentialCommand, UploadCredentialResponse>
{
    public async Task<Result<UploadCredentialResponse>> Handle(UploadCredentialCommand request, CancellationToken cancellationToken)
    {
        var mine = await access.MineAsync(cancellationToken);
        if (mine.IsFailure)
        {
            return Result.Failure<UploadCredentialResponse>(mine.Error);
        }

        var profile = mine.Value;
        var extension = request.ContentType switch { "application/pdf" => "pdf", "image/png" => "png", _ => "jpg" };
        var credentialId = Plastoids.SharedKernel.Domain.Entity.NewId();
        var key = $"credentials/{profile.Id:N}/{credentialId:N}.{extension}";
        var credential = Credential.Submit(profile.TenantId, profile.Id, request.Type, request.Issuer, request.Title, key);
        db.Credentials.Add(credential);

        var now = clock.UtcNow;
        var due = WorkingDays.Add(now, ReviewQueueItem.CredentialSlaWorkingDays);
        db.ReviewQueue.Add(ReviewQueueItem.Enqueue(profile.TenantId, profile.Id, ReviewKinds.Credential, credential.Id, now, due));

        var upload = await storage.GetPresignedUploadUrlAsync(key, request.ContentType, TimeSpan.FromHours(1), cancellationToken);
        return new UploadCredentialResponse(credential.Id, upload, due);
    }
}
