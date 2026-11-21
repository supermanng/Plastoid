using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Tutors.Contracts;
using Plastoids.Modules.Tutors.Domain;
using Plastoids.Modules.Tutors.Persistence;

namespace Plastoids.Modules.Tutors.Services;

internal sealed class TutorVerificationQueue(TutorsDbContext db, IUnitOfWork unitOfWork) : ITutorVerificationQueue
{
    public async Task<IReadOnlyList<TutorVerificationItem>> ListPendingAsync(DateTime utcNow, int limit, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var rows = await db.ReviewQueue.AsNoTracking().Where(q => q.CompletedAt == null).OrderBy(q => q.DueAt).Take(Math.Clamp(limit, 1, 200))
            .Join(db.Profiles.AsNoTracking(), q => q.TutorProfileId, p => p.Id, (q, p) => new { q, p })
            .ToListAsync(cancellationToken);
        return rows.Select(x => new TutorVerificationItem(x.q.Id, x.p.Id, x.p.UserId, x.q.Kind, x.q.ReferenceId, x.q.EnqueuedAt, x.q.DueAt, x.q.DueAt < utcNow, x.p.Status.ToString().ToLowerInvariant())).ToList();
    }

    public async Task<IReadOnlyList<TutorAdminSummary>> ListTutorsAsync(string? status, int limit, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var query = db.Profiles.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TutorStatus>(status, true, out var parsed))
        {
            query = query.Where(p => p.Status == parsed);
        }

        var rows = await query.OrderByDescending(p => p.CreatedAt).Take(Math.Clamp(limit, 1, 200)).ToListAsync(cancellationToken);
        return rows.Select(p => new TutorAdminSummary(p.Id, p.UserId, p.Status.ToString().ToLowerInvariant(), p.Level.ToString().ToLowerInvariant(), p.IdentityPassed, p.QualityScore, p.SessionsDelivered, p.VerifiedAt)).ToList();
    }

    private async Task EnlistAsync(CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }
    }
}
