using Microsoft.Extensions.Options;
using Plastoids.Modules.Payments.Contracts;

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
}

/// <summary>Resolved at sale time and stored on the transaction row (blueprint §18 fee rules). Pro tiers arrive with subscriptions.</summary>
internal sealed class FeeRules(IOptions<FeeOptions> options) : IFeeRules
{
    public FeeDecision Resolve(SaleContext context)
    {
        var o = options.Value;
        return context.Purpose switch
        {
            PaymentPurposes.Booking => new FeeDecision(o.LiveTutoringBps, "live_tutoring"),
            PaymentPurposes.CoPay => new FeeDecision(o.CoPayBps, "co_pay"),
            _ when context.LearnerSource == LearnerSources.TutorReferred => new FeeDecision(o.CourseTutorReferredBps, "course_tutor_referred"),
            _ => new FeeDecision(o.CoursePlatformBps, "course_platform"),
        };
    }
}
