using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Sponsorship.Domain;

public enum SeatBlockStatus
{
    Active = 0,
    Closed = 1,
}

public enum SeatAllocationStatus
{
    Invited = 0,
    Enrolled = 1,
}

/// <summary>How the sponsor named the learner (ADR 0021): by phone or by email.</summary>
public static class ContactKinds
{
    public const string Phone = "phone";
    public const string Email = "email";
}

/// <summary>How one seat's price splits when it is allocated (entry 15): platform commission, the engaged tutor's share and what the payee (tutor or organisation) nets.</summary>
public sealed record SeatSplit(Money Commission, Money TutorShare, Money PayeeNet, string PayeeType, Guid PayeeId);

/// <summary>The cohort a block is bought into, copied from Lms and Catalogue at purchase time so the block reads without cross-module joins.</summary>
public sealed record SeatBlockCohort(Guid CohortId, string CohortName, Guid CourseId, string CourseTitle, string CourseSlug, Guid OfferingId, Guid? TutorUserId, DateTime StartsAt, DateTime EndsAt);

/// <summary>
/// One purchase of N seats in one cohort by one organisation (ADR 0021). Units are redeemed from the organisation wallet
/// for Seats × PricePerSeat (entry 14); seats are given to learners one at a time (entry 15) or released back (entry 16).
/// </summary>
[Audited]
public sealed class SeatBlock : Entity
{
    public const int MaxSeats = 1000;

    private SeatBlock()
    {
    }

    private SeatBlock(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid OrganisationId { get; private set; }
    public string SponsorName { get; private set; } = string.Empty;
    public Guid CohortId { get; private set; }
    public string CohortName { get; private set; } = string.Empty;
    public Guid CourseId { get; private set; }
    public string CourseTitle { get; private set; } = string.Empty;
    public string CourseSlug { get; private set; } = string.Empty;
    public Guid OfferingId { get; private set; }
    public Guid? TutorUserId { get; private set; }
    public DateTime StartsAt { get; private set; }
    public DateTime EndsAt { get; private set; }
    public int Seats { get; private set; }
    public int Allocated { get; private set; }
    public int Released { get; private set; }
    public Money PricePerSeat { get; private set; }
    public long UnitsRedeemed { get; private set; }
    public long UnitRateKobo { get; private set; }

    /// <summary>The naira the wallet released for the units (first-in, first-out lot values), which is what entry 14 moves out of 2050.</summary>
    public Money FundsValue { get; private set; }
    public Guid WalletId { get; private set; }
    public SeatBlockStatus Status { get; private set; }
    public Guid PurchasedBy { get; private set; }
    public DateTime PurchasedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public Guid? LedgerEntryId { get; private set; }

    /// <summary>Seats paid for and neither given to a learner nor released.</summary>
    public int Open => Seats - Allocated - Released;

    /// <summary>Seats × price: what the block is worth in programme funds held (2020).</summary>
    public Money TotalPrice => new(checked(PricePerSeat.AmountKobo * Seats), PricePerSeat.Currency);

    /// <summary>Price of <paramref name="seats"/> seats at the block's price.</summary>
    public Money PriceOf(int seats) => new(checked(PricePerSeat.AmountKobo * seats), PricePerSeat.Currency);

    public static Result<SeatBlock> Purchase(Guid tenantId, Guid organisationId, string sponsorName, SeatBlockCohort cohort, int seats, Money pricePerSeat, Guid purchasedBy, DateTime utcNow)
    {
        if (seats is < 1 or > MaxSeats)
        {
            return Error.Validation("seats_invalid", $"Buy between 1 and {MaxSeats} seats.");
        }

        if (pricePerSeat.IsNegative)
        {
            return Error.Validation("price_invalid", "The seat price cannot be negative.");
        }

        return new SeatBlock(NewId(), tenantId)
        {
            OrganisationId = organisationId,
            SponsorName = sponsorName.Trim(),
            CohortId = cohort.CohortId,
            CohortName = cohort.CohortName,
            CourseId = cohort.CourseId,
            CourseTitle = cohort.CourseTitle,
            CourseSlug = cohort.CourseSlug,
            OfferingId = cohort.OfferingId,
            TutorUserId = cohort.TutorUserId,
            StartsAt = cohort.StartsAt,
            EndsAt = cohort.EndsAt,
            Seats = seats,
            PricePerSeat = pricePerSeat,
            FundsValue = Money.Zero(pricePerSeat.Currency),
            Status = SeatBlockStatus.Active,
            PurchasedBy = purchasedBy,
            PurchasedAt = utcNow,
        };
    }

    /// <summary>Records the wallet redemption that paid for the block; entry 14 follows via <see cref="LinkLedgerEntry"/>.</summary>
    public void Fund(long unitsRedeemed, long unitRateKobo, Money fundsValue, Guid walletId)
    {
        UnitsRedeemed = unitsRedeemed;
        UnitRateKobo = unitRateKobo;
        FundsValue = fundsValue;
        WalletId = walletId;
    }

    public void LinkLedgerEntry(Guid ledgerEntryId) => LedgerEntryId = ledgerEntryId;

    /// <summary>Takes the next open seat; returns its seat number (1-based, never reused because only open seats are ever released).</summary>
    public Result<int> TakeSeat()
    {
        if (Status != SeatBlockStatus.Active)
        {
            return Error.Conflict("block_closed", "The seat block is closed.");
        }

        if (Open <= 0)
        {
            return Error.Conflict("no_open_seats", "Every seat in this block has been allocated or released.");
        }

        Allocated++;
        return Allocated;
    }

    /// <summary>Gives back <paramref name="seats"/> open seats (the sponsor's choice, or the close-out when the cohort ends).</summary>
    public Result Release(int seats)
    {
        if (Status != SeatBlockStatus.Active)
        {
            return Error.Conflict("block_closed", "The seat block is closed.");
        }

        if (seats < 1)
        {
            return Error.Validation("seats_invalid", "Release at least one seat.");
        }

        if (seats > Open)
        {
            return Error.Conflict("release_exceeds_open", $"Only {Open} seats are open in this block.");
        }

        Released += seats;
        return Result.Success();
    }

    /// <summary>Units attributable to <paramref name="seats"/> seats, pro rata to the units redeemed for the whole block (half rounds to even).</summary>
    public long UnitsFor(int seats) => Seats == 0 ? 0 : (long)Math.Round((decimal)UnitsRedeemed * seats / Seats, 0, MidpointRounding.ToEven);

    public void Close(DateTime utcNow)
    {
        if (Status == SeatBlockStatus.Closed)
        {
            return;
        }

        Status = SeatBlockStatus.Closed;
        ClosedAt = utcNow;
    }
}

/// <summary>
/// One seat of a block given to one learner (ADR 0021). Invited until the named phone or email has an account, then
/// enrolled: entry 15 posted, Lms enrols, Payouts records earnings. Holds the phone blind index and the lower-case email, never the number.
/// </summary>
[Audited]
public sealed class SeatAllocation : Entity
{
    private SeatAllocation()
    {
    }

    private SeatAllocation(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid BlockId { get; private set; }
    public int SeatNo { get; private set; }
    public SeatAllocationStatus Status { get; private set; }
    public Guid? LearnerId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string ContactKind { get; private set; } = ContactKinds.Phone;
    public string? PhoneHash { get; private set; }
    public string? Email { get; private set; }
    public string ContactMasked { get; private set; } = string.Empty;
    public DateTime InvitedAt { get; private set; }
    public DateTime? EnrolledAt { get; private set; }
    public Guid? EnrolmentId { get; private set; }
    public Guid? LedgerEntryId { get; private set; }
    public Money Commission { get; private set; }
    public Money TutorShare { get; private set; }
    public Money PayeeNet { get; private set; }
    public string? PayeeType { get; private set; }
    public Guid? PayeeId { get; private set; }
    public Guid AllocatedBy { get; private set; }

    public static SeatAllocation Invite(Guid tenantId, SeatBlock block, int seatNo, string displayName, string contactKind, string? phoneHash, string? email, string contactMasked, Guid allocatedBy, DateTime utcNow)
    {
        var currency = block.PricePerSeat.Currency;
        return new SeatAllocation(NewId(), tenantId)
        {
            BlockId = block.Id,
            SeatNo = seatNo,
            Status = SeatAllocationStatus.Invited,
            DisplayName = displayName.Trim(),
            ContactKind = contactKind,
            PhoneHash = phoneHash,
            Email = email,
            ContactMasked = contactMasked,
            InvitedAt = utcNow,
            Commission = Money.Zero(currency),
            TutorShare = Money.Zero(currency),
            PayeeNet = Money.Zero(currency),
            AllocatedBy = allocatedBy,
        };
    }

    /// <summary>Marks the seat enrolled with its split; <paramref name="ledgerEntryId"/> may follow via <see cref="LinkLedgerEntry"/> once entry 15 is posted from the split.</summary>
    public Result Enrol(Guid learnerId, SeatSplit split, Guid? ledgerEntryId, DateTime utcNow)
    {
        if (Status == SeatAllocationStatus.Enrolled)
        {
            return LearnerId == learnerId ? Result.Success() : Error.Conflict("seat_already_enrolled", "The seat is already enrolled by another learner.");
        }

        Status = SeatAllocationStatus.Enrolled;
        LearnerId = learnerId;
        Commission = split.Commission;
        TutorShare = split.TutorShare;
        PayeeNet = split.PayeeNet;
        PayeeType = split.PayeeType;
        PayeeId = split.PayeeId;
        LedgerEntryId = ledgerEntryId;
        EnrolledAt = utcNow;
        return Result.Success();
    }

    public void LinkEnrolment(Guid enrolmentId) => EnrolmentId = enrolmentId;

    public void LinkLedgerEntry(Guid ledgerEntryId) => LedgerEntryId = ledgerEntryId;
}
