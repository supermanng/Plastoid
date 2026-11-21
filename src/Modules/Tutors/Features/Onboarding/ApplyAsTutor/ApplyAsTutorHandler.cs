using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Tutors.Domain;
using Plastoids.Modules.Tutors.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Tutors.Features.Onboarding.ApplyAsTutor;

internal sealed class ApplyAsTutorHandler(TutorsDbContext db, IPlatformRoles roles, ICurrentUser currentUser, ICurrentTenant tenant, IClock clock)
    : ICommandHandler<ApplyAsTutorCommand, ApplyAsTutorResponse>
{
    public async Task<Result<ApplyAsTutorResponse>> Handle(ApplyAsTutorCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        if (await db.Profiles.AnyAsync(p => p.UserId == userId, cancellationToken))
        {
            return Error.Conflict("already_applied", "You already have a tutor profile.");
        }

        var created = TutorProfile.Apply(tenant.TenantId, userId, request.Headline, request.Bio, request.Subjects, request.Languages, request.Formats, request.TimeZone, request.HourlyRate, request.AcceptCodeOfConduct, clock.UtcNow);
        if (created.IsFailure)
        {
            return Result.Failure<ApplyAsTutorResponse>(created.Error);
        }

        db.Profiles.Add(created.Value);
        // The platform "tutor" role is granted now so the applicant can use /tutors/me/* (all of which require MFA).
        await roles.GrantAsync(userId, RoleNames.Tutor, userId, cancellationToken);
        return new ApplyAsTutorResponse(created.Value.Id, created.Value.Status.ToString().ToLowerInvariant());
    }
}
