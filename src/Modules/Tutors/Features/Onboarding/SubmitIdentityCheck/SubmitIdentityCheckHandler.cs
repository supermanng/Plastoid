using Plastoids.Infrastructure.Integrations;
using Plastoids.Modules.Tutors.Domain;
using Plastoids.Modules.Tutors.Persistence;
using Plastoids.Modules.Tutors.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Tutors.Features.Onboarding.SubmitIdentityCheck;

internal sealed class SubmitIdentityCheckHandler(TutorsDbContext db, TutorAccess access, IKycProvider kyc, IOutbox outbox, IClock clock)
    : ICommandHandler<SubmitIdentityCheckCommand, SubmitIdentityCheckResponse>
{
    public async Task<Result<SubmitIdentityCheckResponse>> Handle(SubmitIdentityCheckCommand request, CancellationToken cancellationToken)
    {
        var mine = await access.MineAsync(cancellationToken);
        if (mine.IsFailure)
        {
            return Result.Failure<SubmitIdentityCheckResponse>(mine.Error);
        }

        var profile = mine.Value;
        if (profile.IdentityPassed)
        {
            return Error.Conflict("identity_already_verified", "Identity is already verified.");
        }

        var result = await kyc.VerifyAsync(new KycRequest(request.Nin, request.Bvn, request.SelfieBlobKey, request.FullName), cancellationToken);
        if (result.IsFailure)
        {
            return Result.Failure<SubmitIdentityCheckResponse>(result.Error);
        }

        var outcome = result.Value;
        db.Checks.Add(VerificationCheck.Record(profile.TenantId, profile.Id, CheckTypes.Identity, outcome.Provider, outcome.Reference,
            outcome.Passed ? CheckResult.Passed : CheckResult.Failed, outcome.FailureReason, null, clock.UtcNow));
        profile.RecordIdentityResult(outcome.Passed, outcome.VerifiedFullName, clock.UtcNow);
        VerificationProgress.Advance(profile, outbox, clock.UtcNow);
        return new SubmitIdentityCheckResponse(outcome.Passed, outcome.Provider, outcome.Reference, outcome.FailureReason);
    }
}
