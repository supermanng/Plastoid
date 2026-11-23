using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Lms.Domain;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Lms.Features.Cohorts.ListCohorts;

/// <summary>Upcoming cohorts (ADR 0021): endsAt ≥ from (default now), ordered by startsAt; optional course filter.</summary>
public sealed record ListCohortsQuery(Guid? CourseId, DateTime? From, int? Limit) : IQuery<IReadOnlyList<CohortListing>>;

public sealed record GetCohortQuery(Guid CohortId) : IQuery<CohortListing>;

/// <summary>A cohort as sponsors and learners see it: the course, the dates, the seats left and the price per seat (the cohort offering's price).</summary>
public sealed record CohortListing(
    Guid CohortId,
    Guid CourseId,
    string CourseSlug,
    string CourseTitle,
    Guid OfferingId,
    string Name,
    DateTime StartsAt,
    DateTime EndsAt,
    int Capacity,
    int Enrolled,
    int Reserved,
    int SeatsLeft,
    Money PricePerSeat,
    Guid TutorUserId,
    string OwnerType,
    string? ProviderName);

internal sealed class ListCohortsHandler(LmsDbContext db, CohortListings listings, IClock clock) : IQueryHandler<ListCohortsQuery, IReadOnlyList<CohortListing>>
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    public async Task<Result<IReadOnlyList<CohortListing>>> Handle(ListCohortsQuery request, CancellationToken cancellationToken)
    {
        var from = request.From ?? clock.UtcNow;
        var limit = Math.Clamp(request.Limit ?? DefaultLimit, 1, MaxLimit);
        var query = db.Cohorts.AsNoTracking().Where(c => c.EndsAt >= from);
        if (request.CourseId is { } courseId)
        {
            query = query.Where(c => c.CourseId == courseId);
        }

        var cohorts = await query.OrderBy(c => c.StartsAt).ThenBy(c => c.Id).Take(limit).ToListAsync(cancellationToken);
        var result = new List<CohortListing>(cohorts.Count);
        foreach (var cohort in cohorts)
        {
            var listing = await listings.BuildAsync(cohort, cancellationToken);
            if (listing is not null)
            {
                result.Add(listing);
            }
        }

        return result;
    }
}

internal sealed class GetCohortHandler(LmsDbContext db, CohortListings listings) : IQueryHandler<GetCohortQuery, CohortListing>
{
    public async Task<Result<CohortListing>> Handle(GetCohortQuery request, CancellationToken cancellationToken)
    {
        var cohort = await db.Cohorts.AsNoTracking().SingleOrDefaultAsync(c => c.Id == request.CohortId, cancellationToken);
        if (cohort is null)
        {
            return Error.NotFound("cohort_not_found", "No cohort with that id.");
        }

        var listing = await listings.BuildAsync(cohort, cancellationToken);
        if (listing is null)
        {
            return Error.NotFound("cohort_not_found", "The cohort's course is not published.");
        }

        return listing;
    }
}

/// <summary>Joins a cohort to its published course (Catalogue) and provider (Identity). Unpublished courses yield null so their cohorts are skipped.</summary>
internal sealed class CohortListings(ICourseOutlineReader courses, IOrganisationDirectory organisations)
{
    private readonly Dictionary<Guid, PublishedCourse?> courseCache = [];
    private readonly Dictionary<Guid, string?> providerCache = [];

    public async Task<CohortListing?> BuildAsync(Cohort cohort, CancellationToken cancellationToken)
    {
        if (!courseCache.TryGetValue(cohort.CourseId, out var course))
        {
            course = await courses.GetPublishedAsync(cohort.CourseId, null, cancellationToken);
            courseCache[cohort.CourseId] = course;
        }

        if (course is null)
        {
            return null;
        }

        var offering = course.Offerings.SingleOrDefault(o => o.OfferingId == cohort.OfferingId);
        if (offering is null)
        {
            return null;
        }

        string? providerName = null;
        if (course.OrganisationId is { } organisationId)
        {
            if (!providerCache.TryGetValue(organisationId, out providerName))
            {
                providerName = (await organisations.GetAsync(organisationId, cancellationToken))?.Name;
                providerCache[organisationId] = providerName;
            }
        }

        return new CohortListing(cohort.Id, course.CourseId, course.Slug, course.Title, offering.OfferingId, cohort.Name, cohort.StartsAt, cohort.EndsAt, cohort.Capacity, cohort.Enrolled, cohort.Reserved, cohort.SeatsLeft, offering.Price, cohort.TutorUserId, course.OwnerType, providerName);
    }
}

internal static class ListCohortsEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/", async (Guid? courseId, DateTime? from, int? limit, ISender sender, CancellationToken ct) => (await sender.Send(new ListCohortsQuery(courseId, from, limit), ct)).ToHttpResult())
            .WithName("ListCohorts")
            .WithSummary("Upcoming cohorts with seats left and the price per seat (ADR 0021): courseId, from (default now), limit (default 20, max 100).")
            .AllowAnonymous();

        group.MapGet("/{cohortId:guid}", async (Guid cohortId, ISender sender, CancellationToken ct) => (await sender.Send(new GetCohortQuery(cohortId), ct)).ToHttpResult())
            .WithName("GetCohort")
            .WithSummary("One cohort with seats left and the price per seat.")
            .AllowAnonymous();
    }
}
