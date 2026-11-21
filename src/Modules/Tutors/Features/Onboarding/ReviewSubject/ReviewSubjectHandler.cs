using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Tutors.Domain;
using Plastoids.Modules.Tutors.Features.Onboarding.ReviewCredential;
using Plastoids.Modules.Tutors.Persistence;
using Plastoids.Modules.Tutors.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Tutors.Features.Onboarding.ReviewSubject;

internal sealed class ReviewSubjectHandler(TutorsDbContext db, IOutbox outbox, ICurrentUser currentUser, IClock clock)
    : ICommandHandler<ReviewSubjectCommand, ReviewSubjectResponse>
{
    public async Task<Result<ReviewSubjectResponse>> Handle(ReviewSubjectCommand request, CancellationToken cancellationToken)
    {
        var profile = await db.Profiles.SingleOrDefaultAsync(p => p.Id == request.TutorId, cancellationToken);
        if (profile is null)
        {
            return Error.NotFound("tutor_not_found", "No such tutor.");
        }

        var reviewerId = currentUser.UserId ?? Guid.Empty;
        if (profile.UserId == reviewerId)
        {
            return Error.Forbidden("self_review", "You cannot review your own demo lesson.");
        }

        if (profile.DemoLessonSubmittedAt is null)
        {
            return Error.Validation("demo_lesson_missing", "The tutor has not submitted a demo lesson.");
        }

        if (profile.SubjectPassed)
        {
            return Error.Conflict("subject_already_reviewed", "Subject competence is already confirmed.");
        }

        var now = clock.UtcNow;
        var approved = request.Decision == ReviewDecisions.Approved;
        db.Checks.Add(VerificationCheck.Record(profile.TenantId, profile.Id, CheckTypes.Subject, "reviewer-panel", profile.Id.ToString("N"),
            approved ? CheckResult.Passed : CheckResult.Failed, request.Note ?? (request.Score is { } s ? $"score={s}" : null), reviewerId, now));
        var queueItem = await db.ReviewQueue.SingleOrDefaultAsync(q => q.Kind == ReviewKinds.Subject && q.ReferenceId == profile.Id && q.CompletedAt == null, cancellationToken);
        queueItem?.Complete(reviewerId, now);

        if (approved)
        {
            profile.RecordSubjectReviewed(now);
            VerificationProgress.Advance(profile, outbox, now);
        }
        else
        {
            // A failed demo clears the submission so the tutor can try again.
            profile.Reject();
        }

        return new ReviewSubjectResponse(profile.Id, profile.Status.ToString().ToLowerInvariant(), profile.IsVerified);
    }
}
