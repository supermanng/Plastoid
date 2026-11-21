using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Tutors.Domain;
using Plastoids.Modules.Tutors.Persistence;
using Plastoids.Modules.Tutors.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Tutors.Features.Profile.GetTutorDashboard;

internal sealed class GetTutorDashboardHandler(TutorsDbContext db, TutorAccess access) : IQueryHandler<GetTutorDashboardQuery, TutorDashboard>
{
    public async Task<Result<TutorDashboard>> Handle(GetTutorDashboardQuery request, CancellationToken cancellationToken)
    {
        var mine = await access.MineAsync(cancellationToken, tracking: false);
        if (mine.IsFailure)
        {
            return Result.Failure<TutorDashboard>(mine.Error);
        }

        var p = mine.Value;
        var credentials = await db.Credentials.AsNoTracking().Where(c => c.TutorProfileId == p.Id).OrderBy(c => c.CreatedAt).ToListAsync(cancellationToken);
        var queue = await db.ReviewQueue.AsNoTracking().Where(q => q.TutorProfileId == p.Id && q.CompletedAt == null).ToListAsync(cancellationToken);
        var payout = await db.PayoutAccounts.AsNoTracking().SingleOrDefaultAsync(a => a.TutorProfileId == p.Id, cancellationToken);
        var availabilityCount = await db.Availabilities.CountAsync(a => a.TutorProfileId == p.Id, cancellationToken);
        var priceRuleCount = await db.PriceRules.CountAsync(r => r.TutorProfileId == p.Id, cancellationToken);

        var stages = new List<VerificationStage>
        {
            new("identity", p.IdentityPassed ? "passed" : "pending", p.IdentityVerifiedAt, null),
            new("qualifications", p.QualificationsPassed ? "passed" : credentials.Count == 0 ? "not_started" : "in_review", p.QualificationsReviewedAt, queue.Where(q => q.Kind == ReviewKinds.Credential).Select(q => (DateTime?)q.DueAt).Min()),
            new("subject", p.SubjectPassed ? "passed" : p.DemoLessonSubmittedAt is null ? "not_started" : p.Status == TutorStatus.Rejected ? "failed" : "in_review", p.SubjectReviewedAt, queue.Where(q => q.Kind == ReviewKinds.Subject).Select(q => (DateTime?)q.DueAt).Min()),
            new("payout_account", payout is null ? "not_started" : "passed", payout?.VerifiedAt, null),
        };

        return new TutorDashboard(
            p.Id,
            p.Status.ToString().ToLowerInvariant(),
            p.Level.ToString().ToLowerInvariant(),
            p.Headline,
            p.Subjects,
            p.Formats,
            p.TimeZone,
            p.HourlyRate,
            stages,
            credentials.Select(c => new CredentialSummary(c.Id, c.Type, c.Title, c.Issuer, c.Status.ToString().ToLowerInvariant(), c.ReviewNote)).ToList(),
            availabilityCount,
            priceRuleCount,
            p.CalendarConnected,
            payout?.AccountNumberMasked,
            p.QualityScore,
            p.SessionsDelivered,
            p.EnrolmentsTaught,
            p.RatingAverage);
    }
}
