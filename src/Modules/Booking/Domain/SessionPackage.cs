using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Booking.Domain;

public enum PackageStatus
{
    PendingPayment = 0,
    Active = 1,
    Exhausted = 2,
    Cancelled = 3,
}

/// <summary>Blueprint §9: packages (e.g. 5 sessions) paid once; each booking consumes a session.</summary>
[Audited]
public sealed class SessionPackage : Entity
{
    public static readonly IReadOnlyCollection<int> AllowedSizes = [5, 10];
    public const int DiscountPercentFor10 = 10;

    private SessionPackage()
    {
    }

    private SessionPackage(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid TutorId { get; private set; }
    public Guid TutorUserId { get; private set; }
    public Guid LearnerId { get; private set; }
    public string Format { get; private set; } = string.Empty;
    public int DurationMinutes { get; private set; }
    public int SessionsTotal { get; private set; }
    public int SessionsUsed { get; private set; }
    public Money PricePerSession { get; private set; }
    public Money Total { get; private set; }
    public PackageStatus Status { get; private set; }
    public Guid? PaymentIntentId { get; private set; }
    public DateTime? ActivatedAt { get; private set; }

    public static Result<SessionPackage> Create(Guid tenantId, Guid tutorId, Guid tutorUserId, Guid learnerId, string format, int durationMinutes, int sessions, Money pricePerSession)
    {
        if (!AllowedSizes.Contains(sessions))
        {
            return Error.Validation("package_size_invalid", "Packages come in 5 or 10 sessions.");
        }

        var gross = new Money(checked(pricePerSession.AmountKobo * sessions), pricePerSession.Currency);
        var total = sessions == 10 ? gross - gross.Percent(DiscountPercentFor10, MidpointRounding.ToEven) : gross;
        return new SessionPackage(NewId(), tenantId)
        {
            TutorId = tutorId,
            TutorUserId = tutorUserId,
            LearnerId = learnerId,
            Format = format,
            DurationMinutes = durationMinutes,
            SessionsTotal = sessions,
            PricePerSession = pricePerSession,
            Total = total,
            Status = PackageStatus.PendingPayment,
        };
    }

    public void AttachPayment(Guid paymentIntentId) => PaymentIntentId = paymentIntentId;

    public bool Activate(DateTime utcNow)
    {
        if (Status != PackageStatus.PendingPayment)
        {
            return false;
        }

        Status = PackageStatus.Active;
        ActivatedAt = utcNow;
        return true;
    }

    public int SessionsRemaining => SessionsTotal - SessionsUsed;

    public Result UseSession()
    {
        if (Status != PackageStatus.Active || SessionsRemaining <= 0)
        {
            return Error.Conflict("package_exhausted", "No sessions left on this package.");
        }

        SessionsUsed++;
        if (SessionsRemaining == 0)
        {
            Status = PackageStatus.Exhausted;
        }

        return Result.Success();
    }

    public void ReturnSession()
    {
        if (SessionsUsed > 0)
        {
            SessionsUsed--;
            if (Status == PackageStatus.Exhausted)
            {
                Status = PackageStatus.Active;
            }
        }
    }

    /// <summary>Per-session value actually paid (after the package discount), used for refunds and holds.</summary>
    public Money EffectivePricePerSession => new(Total.AmountKobo / SessionsTotal, Total.Currency);
}
