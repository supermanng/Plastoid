using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Catalogue.Domain;

public static class OfferingFormats
{
    public const string SelfPaced = "self_paced";
    public const string Cohort = "cohort";
    public const string OneToOne = "one_to_one";
    public static readonly IReadOnlyCollection<string> All = [SelfPaced, Cohort, OneToOne];
    public static bool IsValid(string f) => All.Contains(f, StringComparer.Ordinal);
}

public static class FundingTypes
{
    public const string Self = "self";
    public const string Sponsored = "sponsored";
    public const string Employer = "employer";
    public static readonly IReadOnlyCollection<string> All = [Self, Sponsored, Employer];
    public static bool IsValid(string f) => All.Contains(f, StringComparer.Ordinal);
}

/// <summary>How a course is sold: format, price (Money), accepted funding types, capacity and start (blueprint §16).</summary>
[Audited]
public sealed class Offering : Entity
{
    private Offering()
    {
    }

    private Offering(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid CourseId { get; private set; }
    public string Format { get; private set; } = string.Empty;
    public Money Price { get; private set; }
    public List<string> FundingTypes { get; private set; } = [];
    public int? Capacity { get; private set; }
    public DateTime? StartAt { get; private set; }
    public int? DurationWeeks { get; private set; }
    public bool Active { get; private set; }

    public static Result<Offering> Create(Guid tenantId, Guid courseId, string format, Money price, IEnumerable<string> fundingTypes, int? capacity, DateTime? startAt, int? durationWeeks, DateTime utcNow)
    {
        if (price.IsNegative || price.Currency != Money.Naira)
        {
            return Error.Validation("price_invalid", "Price must be a non-negative naira amount.");
        }

        var funding = fundingTypes.Select(f => f.Trim().ToLowerInvariant()).Distinct().ToList();
        if (funding.Count == 0 || funding.Any(f => !Domain.FundingTypes.IsValid(f)))
        {
            return Error.Validation("funding_types_invalid", "fundingTypes must be self, sponsored and/or employer.");
        }

        if (format == OfferingFormats.Cohort && (startAt is null || startAt <= utcNow))
        {
            return Error.Validation("cohort_start_required", "Cohort offerings need a future startAt.");
        }

        if (format != OfferingFormats.SelfPaced && capacity is null)
        {
            return Error.Validation("capacity_required", "Cohort and one-to-one offerings need a capacity.");
        }

        return new Offering(NewId(), tenantId)
        {
            CourseId = courseId,
            Format = format,
            Price = price,
            FundingTypes = funding,
            Capacity = capacity,
            StartAt = startAt,
            DurationWeeks = durationWeeks,
            Active = true,
        };
    }

    public bool AcceptsSponsorship => FundingTypes.Contains(Domain.FundingTypes.Sponsored);

    public void Close() => Active = false;
}
