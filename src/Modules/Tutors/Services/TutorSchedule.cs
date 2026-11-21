using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Tutors.Contracts;
using Plastoids.Modules.Tutors.Domain;
using Plastoids.Modules.Tutors.Persistence;

namespace Plastoids.Modules.Tutors.Services;

internal sealed class TutorSchedule(TutorsDbContext db, IUnitOfWork unitOfWork) : ITutorSchedule
{
    public async Task<TutorScheduleInfo?> GetAsync(Guid tutorId, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var p = await db.Profiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tutorId, cancellationToken);
        if (p is null)
        {
            return null;
        }

        var rules = await db.PriceRules.AsNoTracking().Where(r => r.TutorProfileId == p.Id).ToListAsync(cancellationToken);
        return new TutorScheduleInfo(p.Id, p.UserId, p.IsVerified, p.TimeZone, p.HourlyRate, p.Formats, rules.Select(r => new TutorPriceRule(r.Format, r.DurationMinutes, r.Price, r.MaxLearners)).ToList(), p.CalendarConnected);
    }

    public async Task<IReadOnlyList<SlotWindow>> SlotsAsync(Guid tutorId, DateOnly fromLocal, int days, int slotMinutes, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var p = await db.Profiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == tutorId, cancellationToken);
        if (p is null)
        {
            return [];
        }

        var rules = await db.Availabilities.AsNoTracking().Where(a => a.TutorProfileId == p.Id).ToListAsync(cancellationToken);
        return AvailabilityCalendar.Slots(rules, p.TimeZone, fromLocal, days, slotMinutes).Select(s => new SlotWindow(s.StartUtc, s.EndUtc)).ToList();
    }

    private async Task EnlistAsync(CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }
    }
}
