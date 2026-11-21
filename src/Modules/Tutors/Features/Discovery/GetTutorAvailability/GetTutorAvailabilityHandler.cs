using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Tutors.Domain;
using Plastoids.Modules.Tutors.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Tutors.Features.Discovery.GetTutorAvailability;

internal sealed class GetTutorAvailabilityHandler(TutorsDbContext db, IClock clock) : IQueryHandler<GetTutorAvailabilityQuery, TutorAvailabilityResponse>
{
    public async Task<Result<TutorAvailabilityResponse>> Handle(GetTutorAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var profile = await db.Profiles.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.TutorId && p.Status == TutorStatus.Verified, cancellationToken);
        if (profile is null)
        {
            return Error.NotFound("tutor_not_found", "No verified tutor with that id.");
        }

        var days = Math.Clamp(request.Days ?? 7, 1, 31);
        var tz = TimeZoneInfo.FindSystemTimeZoneById(profile.TimeZone);
        var from = request.From ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.UtcNow, tz));
        var rules = await db.Availabilities.AsNoTracking().Where(a => a.TutorProfileId == profile.Id).ToListAsync(cancellationToken);
        var slots = AvailabilityCalendar.Slots(rules, profile.TimeZone, from, days).Where(s => s.StartUtc > clock.UtcNow).ToList();
        return new TutorAvailabilityResponse(profile.Id, profile.TimeZone, from, days, slots);
    }
}
