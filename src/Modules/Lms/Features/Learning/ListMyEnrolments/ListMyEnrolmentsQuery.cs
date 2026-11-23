using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Lms.Features.Learning.ListMyEnrolments;

public sealed record ListMyEnrolmentsQuery : IQuery<IReadOnlyList<EnrolmentSummary>>;

public sealed record EnrolmentSummary(Guid EnrolmentId, Guid CourseId, Guid OfferingId, Guid? CohortId, string? CourseSlug, int VersionNo, string FundingSource, string Status, decimal ProgressPct, int CompletedLessons, int RequiredLessons, DateTime StartedAt, DateTime? CompletedAt, DateTime ExpiresAt, decimal? FinalScore, string? FundingLabel = null);

internal sealed class ListMyEnrolmentsHandler(LmsDbContext db, ICurrentUser currentUser) : IQueryHandler<ListMyEnrolmentsQuery, IReadOnlyList<EnrolmentSummary>>
{
    public async Task<Result<IReadOnlyList<EnrolmentSummary>>> Handle(ListMyEnrolmentsQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? Guid.Empty;
        var rows = await db.Enrolments.AsNoTracking().Where(e => e.LearnerId == userId).OrderByDescending(e => e.StartedAt).ToListAsync(cancellationToken);
        var courseIds = rows.Select(r => r.CourseId).Distinct().ToList();
        var slugs = await db.PublishedCourses.AsNoTracking().Where(c => courseIds.Contains(c.CourseId)).ToDictionaryAsync(c => c.CourseId, c => c.Slug, cancellationToken);
        return rows.Select(e => new EnrolmentSummary(e.Id, e.CourseId, e.OfferingId, e.CohortId, slugs.GetValueOrDefault(e.CourseId), e.VersionNo, e.FundingSource, e.Status.ToString().ToLowerInvariant(), e.ProgressPct, e.CompletedLessons, e.RequiredLessons, e.StartedAt, e.CompletedAt, e.ExpiresAt, e.FinalScore, e.FundingLabel)).ToList();
    }
}

internal static class ListMyEnrolmentsEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/me", async (ISender sender, CancellationToken ct) => (await sender.Send(new ListMyEnrolmentsQuery(), ct)).ToHttpResult())
            .WithName("ListMyEnrolments")
            .WithSummary("My courses with progress and status.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
