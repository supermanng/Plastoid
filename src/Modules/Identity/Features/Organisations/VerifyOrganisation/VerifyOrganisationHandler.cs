using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Identity.Features.Organisations.VerifyOrganisation;

internal sealed class VerifyOrganisationHandler(IdentityDbContext db, IOutbox outbox, ICurrentUser currentUser, IClock clock) : ICommandHandler<VerifyOrganisationCommand, VerifyOrganisationResponse>
{
    public async Task<Result<VerifyOrganisationResponse>> Handle(VerifyOrganisationCommand request, CancellationToken cancellationToken)
    {
        var staffId = currentUser.UserId ?? Guid.Empty;
        var organisation = await db.Organisations.SingleOrDefaultAsync(o => o.Id == request.OrganisationId, cancellationToken);
        if (organisation is null)
        {
            return Error.NotFound("organisation_not_found", "No such organisation.");
        }

        // Four-eyes: staff who belong to the organisation cannot verify it.
        if (await db.Memberships.AnyAsync(m => m.UserId == staffId && m.OrganisationId == organisation.Id, cancellationToken))
        {
            return Error.Forbidden("self_verification", "Members of an organisation cannot verify it.");
        }

        var decision = request.Decision == VerificationDecisions.Verified
            ? organisation.Verify(staffId, clock.UtcNow)
            : organisation.Reject(staffId, request.Reason!, clock.UtcNow);
        if (decision.IsFailure)
        {
            return Result.Failure<VerifyOrganisationResponse>(decision.Error);
        }

        if (organisation.IsVerified)
        {
            outbox.Enqueue(new OrganisationVerified(organisation.Id, organisation.Id, organisation.Type.ToString().ToLowerInvariant()));
        }

        return new VerifyOrganisationResponse(organisation.Id, organisation.VerificationStatus.ToString().ToLowerInvariant());
    }
}
