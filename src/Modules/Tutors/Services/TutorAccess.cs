using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Tutors.Domain;
using Plastoids.Modules.Tutors.Persistence;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Tutors.Services;

/// <summary>Resolves the caller's tutor profile; the common first step of every /tutors/me slice.</summary>
internal sealed class TutorAccess(TutorsDbContext db, ICurrentUser currentUser)
{
    public static readonly Error NoProfile = Error.NotFound("tutor_profile_not_found", "Apply as a tutor first.");

    public async Task<Result<TutorProfile>> MineAsync(CancellationToken cancellationToken, bool tracking = true)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var query = tracking ? db.Profiles : db.Profiles.AsNoTracking();
        var profile = await query.SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return profile is null ? NoProfile : profile;
    }
}
