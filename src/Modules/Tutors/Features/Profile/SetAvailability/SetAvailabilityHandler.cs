using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Tutors.Domain;
using Plastoids.Modules.Tutors.Persistence;
using Plastoids.Modules.Tutors.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Tutors.Features.Profile.SetAvailability;

internal sealed class SetAvailabilityHandler(TutorsDbContext db, TutorAccess access) : ICommandHandler<SetAvailabilityCommand, SetAvailabilityResponse>
{
    public async Task<Result<SetAvailabilityResponse>> Handle(SetAvailabilityCommand request, CancellationToken cancellationToken)
    {
        var mine = await access.MineAsync(cancellationToken);
        if (mine.IsFailure)
        {
            return Result.Failure<SetAvailabilityResponse>(mine.Error);
        }

        var profile = mine.Value;
        var tz = profile.SetTimeZone(request.TimeZone);
        if (tz.IsFailure)
        {
            return Result.Failure<SetAvailabilityResponse>(tz.Error);
        }

        db.Availabilities.RemoveRange(await db.Availabilities.Where(a => a.TutorProfileId == profile.Id).ToListAsync(cancellationToken));
        foreach (var w in request.Weekly)
        {
            db.Availabilities.Add(Availability.Weekly(profile.TenantId, profile.Id, w.Day, w.Start, w.End));
        }

        foreach (var e in request.Exceptions)
        {
            db.Availabilities.Add(Availability.Exception(profile.TenantId, profile.Id, e.Date, e.Start ?? TimeOnly.MinValue, e.End ?? new TimeOnly(23, 59, 59), e.Available));
        }

        return new SetAvailabilityResponse(request.Weekly.Count, request.Exceptions.Count, profile.TimeZone);
    }
}
