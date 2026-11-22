using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payments.Domain;
using Plastoids.Modules.Payments.Persistence;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payments.Services;

public sealed class FeeOptions
{
    public const string SectionName = "Fees";

    /// <summary>Course sale when Plastoids found the learner (blueprint §5: 20%).</summary>
    public int CoursePlatformBps { get; set; } = 2000;

    /// <summary>Course sale through the tutor's own link (8%).</summary>
    public int CourseTutorReferredBps { get; set; } = 800;

    /// <summary>Live 1:1 / group tutoring (15%).</summary>
    public int LiveTutoringBps { get; set; } = 1500;

    /// <summary>Sponsored co-pay: no commission (fees are invoiced to the sponsor).</summary>
    public int CoPayBps { get; set; }

    /// <summary>Sale of an organisation-owned course (ADR 0019: 15%); the tutor share comes out of the organisation's net.</summary>
    public int CourseProviderBps { get; set; } = 1500;
}

/// <summary>
/// Resolved at sale time and stored on the transaction row (blueprint §18 fee rules). An approved, versioned fee rule
/// effective at the sale instant wins; otherwise the configured defaults apply. Pro tiers arrive with subscriptions.
/// </summary>
internal sealed class FeeRules(PaymentsDbContext db, IUnitOfWork unitOfWork, IOptions<FeeOptions> options, IClock clock) : IFeeRules
{
    private List<FeeRule>? _approved;

    public FeeDecision Resolve(SaleContext context)
    {
        var code = CodeFor(context);
        var rule = FeeRule.EffectiveAt(Approved(), code, clock.UtcNow);
        return rule is null ? new FeeDecision(DefaultBps(code, options.Value), code) : new FeeDecision(rule.CommissionBps, $"{code}:v{rule.Version}");
    }

    public static string CodeFor(SaleContext context) => context.Purpose switch
    {
        PaymentPurposes.Booking => "live_tutoring",
        PaymentPurposes.CoPay => "co_pay",
        PaymentPurposes.Units => "units",
        _ when context.ProviderSale => "course_provider", // the referral rate is a tutor's; a provider sale is not tutor-referred
        _ when context.LearnerSource == LearnerSources.TutorReferred => "course_tutor_referred",
        _ => "course_platform",
    };

    public static int DefaultBps(string code, FeeOptions o) => code switch
    {
        "live_tutoring" => o.LiveTutoringBps,
        "co_pay" => o.CoPayBps,
        "course_tutor_referred" => o.CourseTutorReferredBps,
        "course_provider" => o.CourseProviderBps,
        "units" => 0,
        _ => o.CoursePlatformBps,
    };

    private List<FeeRule> Approved()
    {
        if (_approved is not null)
        {
            return _approved;
        }

        // IFeeRules is synchronous by contract (resolved inline at sale time); the approved set is small and read once per scope.
        if (unitOfWork.IsActive)
        {
            unitOfWork.EnlistAsync(db, CancellationToken.None).GetAwaiter().GetResult();
        }

        _approved = db.FeeRules.AsNoTracking().Where(r => r.Status == FeeRuleStatus.Approved).ToList();
        return _approved;
    }
}
