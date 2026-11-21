using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Tutors.Domain;
using Plastoids.Modules.Tutors.Persistence;
using Plastoids.Modules.Tutors.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Tutors.Features.Onboarding.ReviewCredential;

internal sealed class ReviewCredentialHandler(TutorsDbContext db, IOutbox outbox, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<ReviewCredentialCommand, ReviewCredentialResponse>
{
    public async Task<Result<ReviewCredentialResponse>> Handle(ReviewCredentialCommand request, CancellationToken cancellationToken)
    {
        var credential = await db.Credentials.SingleOrDefaultAsync(c => c.Id == request.CredentialId, cancellationToken);
        if (credential is null)
        {
            return Error.NotFound("credential_not_found", "No such credential.");
        }

        var profile = await db.Profiles.SingleAsync(p => p.Id == credential.TutorProfileId, cancellationToken);
        var reviewerId = currentUser.UserId ?? Guid.Empty;
        if (profile.UserId == reviewerId)
        {
            return Error.Forbidden("self_review", "You cannot review your own credentials.");
        }

        var now = clock.UtcNow;
        var reviewed = credential.Review(request.Decision == ReviewDecisions.Approved, reviewerId, request.Note, now);
        if (reviewed.IsFailure)
        {
            return Result.Failure<ReviewCredentialResponse>(reviewed.Error);
        }

        var queueItem = await db.ReviewQueue.SingleOrDefaultAsync(q => q.Kind == ReviewKinds.Credential && q.ReferenceId == credential.Id && q.CompletedAt == null, cancellationToken);
        queueItem?.Complete(reviewerId, now);
        db.Checks.Add(VerificationCheck.Record(profile.TenantId, profile.Id, CheckTypes.Qualifications, "reviewer", credential.Id.ToString("N"),
            credential.Status == CredentialStatus.Approved ? CheckResult.Passed : CheckResult.Failed, request.Note, reviewerId, now));

        // Stage 2 passes once at least one credential is approved and none is still pending.
        var credentials = await db.Credentials.Where(c => c.TutorProfileId == profile.Id).ToListAsync(cancellationToken);
        var passed = credentials.Any(c => c.Status == CredentialStatus.Approved) && credentials.All(c => c.Status != CredentialStatus.Pending);
        if (passed && !profile.QualificationsPassed)
        {
            profile.RecordQualificationsReviewed(now);
            VerificationProgress.Advance(profile, outbox, now);
        }

        return new ReviewCredentialResponse(credential.Id, credential.Status.ToString().ToLowerInvariant(), profile.QualificationsPassed);
    }
}
