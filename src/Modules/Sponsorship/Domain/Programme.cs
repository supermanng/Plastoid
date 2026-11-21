using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Sponsorship.Domain;

public enum ProgrammeStatus
{
    Draft = 0,
    Quoted = 1,
    QuoteApproved = 2,
    Published = 3,
    Closed = 4,
}

public enum ReviewStatus
{
    Pending = 0,
    Approved = 1,
    ChangesRequested = 2,
    Rejected = 3,
}

public static class SelectionModes
{
    public const string Plastoids = "plastoids";
    public const string Sponsor = "sponsor";
    public const string Delegated = "delegated";
    public static readonly IReadOnlyCollection<string> All = [Plastoids, Sponsor, Delegated];
    public static bool IsValid(string m) => All.Contains(m, StringComparer.Ordinal);
}

public static class UnusedFundsPolicies
{
    public const string Refund = "refund";
    public const string Rollover = "rollover";
    public static bool IsValid(string p) => p is Refund or Rollover;
}

/// <summary>Blueprint §5 stream 5: management fee 12 % of tuition (minimum ₦3m per programme) and a ₦12,000 platform seat fee; VAT 7.5 % on the fees only.</summary>
public static class FeeSchedule
{
    public const int ManagementFeeBps = 1200;
    public static readonly Money MinimumManagementFee = Money.NairaAmount(3_000_000);
    public static readonly Money SeatFee = Money.NairaAmount(12_000);
    public const int VatBps = 750;
}

/// <summary>The quote lines (story F1): tuition, management fee, seat fee and VAT.</summary>
public sealed record QuoteLines(int Seats, Money TuitionPerSeatSponsorShare, Money Tuition, Money ManagementFee, Money SeatFee, Money Vat, Money Total)
{
    public static QuoteLines Compute(int seats, Money tuitionPerSeat, int sponsorSharePercent)
    {
        var share = tuitionPerSeat.Percent(sponsorSharePercent, MidpointRounding.ToEven);
        var tuition = new Money(checked(share.AmountKobo * seats), share.Currency);
        var managementFee = tuition.Percent(FeeSchedule.ManagementFeeBps / 100m, MidpointRounding.ToEven);
        if (managementFee < FeeSchedule.MinimumManagementFee)
        {
            managementFee = FeeSchedule.MinimumManagementFee;
        }

        var seatFee = new Money(checked(FeeSchedule.SeatFee.AmountKobo * seats), share.Currency);
        var vat = (managementFee + seatFee).Percent(FeeSchedule.VatBps / 100m, MidpointRounding.ToEven);
        return new QuoteLines(seats, share, tuition, managementFee, seatFee, vat, tuition + managementFee + seatFee + vat);
    }

    /// <summary>Management fee attributable to one seat (entry 8 is charged per filled seat per month).</summary>
    public Money ManagementFeePerSeat => new(Seats == 0 ? 0 : ManagementFee.AmountKobo / Seats, ManagementFee.Currency);

    /// <summary>What an unfilled seat gives back at close-out (entry 10): tuition share + management fee share + seat fee, before VAT.</summary>
    public Money RefundPerUnfilledSeat => TuitionPerSeatSponsorShare + ManagementFeePerSeat + FeeSchedule.SeatFee;

    /// <summary>Invoice value of one seat including VAT: used to unlock seats pro rata to funds received.</summary>
    public long TotalPerSeatKobo => Seats == 0 ? 0 : Total.AmountKobo / Seats;
}

public sealed record ProgrammeDraft(
    string? Objectives,
    Guid? CourseId,
    Guid? OfferingId,
    string? CourseTitle,
    Guid? TutorUserId,
    int? SeatCount,
    Money? TuitionPerSeat,
    int? SponsorSharePercent,
    Money? Budget,
    string? EligibilityRulesJson,
    string? EntryAssessmentJson,
    string? ScoringRubricJson,
    DateTime? ApplicationsOpenAt,
    DateTime? ApplicationsCloseAt,
    DateTime? StartsAt,
    DateTime? EndsAt,
    string? SelectionMode,
    string? UnusedFundsPolicy);

/// <summary>A sponsored programme / campaign (blueprint §10): quoted, approved, reviewed, funded, published, closed.</summary>
[Audited]
public sealed class Programme : Entity
{
    private Programme()
    {
    }

    private Programme(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid OrganisationId { get; private set; }
    public string SponsorName { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Objectives { get; private set; }
    public Guid? CourseId { get; private set; }
    public Guid? OfferingId { get; private set; }
    public string? CourseTitle { get; private set; }
    public Guid? TutorUserId { get; private set; }
    public int SeatCount { get; private set; }
    public Money TuitionPerSeat { get; private set; } = Money.Zero();
    public int SponsorSharePercent { get; private set; } = 100;
    public Money Budget { get; private set; } = Money.Zero();
    public string EligibilityRulesJson { get; private set; } = "{}";
    public string? EntryAssessmentJson { get; private set; }
    public string? ScoringRubricJson { get; private set; }
    public DateTime? ApplicationsOpenAt { get; private set; }
    public DateTime? ApplicationsCloseAt { get; private set; }
    public DateTime? StartsAt { get; private set; }
    public DateTime? EndsAt { get; private set; }
    public string SelectionMode { get; private set; } = SelectionModes.Plastoids;
    public string UnusedFundsPolicy { get; private set; } = UnusedFundsPolicies.Refund;
    public ProgrammeStatus Status { get; private set; }
    public int QuoteVersion { get; private set; }
    public Money QuoteTuitionPerSeatShare { get; private set; } = Money.Zero();
    public Money QuoteTuition { get; private set; } = Money.Zero();
    public Money QuoteManagementFee { get; private set; } = Money.Zero();
    public Money QuoteSeatFee { get; private set; } = Money.Zero();
    public Money QuoteVat { get; private set; } = Money.Zero();
    public Money QuoteTotal { get; private set; } = Money.Zero();
    public DateTime? QuotedAt { get; private set; }
    public DateTime? QuoteApprovedAt { get; private set; }
    public Guid? QuoteApprovedBy { get; private set; }
    public Guid? InvoiceId { get; private set; }
    public ReviewStatus ReviewStatus { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public bool? RulesLawful { get; private set; }
    public string? ReviewNotes { get; private set; }
    public bool PaymentPlanApproved { get; private set; }
    public string? PaymentPlanJson { get; private set; }
    public Money FundedAmount { get; private set; } = Money.Zero();
    public DateTime? FundedAt { get; private set; }
    public int SeatsUnlocked { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public Guid? PublishedBy { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public Guid? ClosedBy { get; private set; }
    public Money CloseoutRefund { get; private set; } = Money.Zero();
    public Guid CreatedBy { get; private set; }

    public static Programme Create(Guid tenantId, Guid organisationId, string sponsorName, string name, Guid createdBy)
        => new(NewId(), tenantId) { OrganisationId = organisationId, SponsorName = sponsorName.Trim(), Name = name.Trim(), CreatedBy = createdBy, Status = ProgrammeStatus.Draft };

    public bool IsEditable => Status is ProgrammeStatus.Draft or ProgrammeStatus.Quoted;

    /// <summary>Stepper save (story F1): any subset of fields; a saved change after a quote sends the programme back to Draft so the quote is recomputed.</summary>
    public Result SaveDraft(string? name, ProgrammeDraft draft)
    {
        if (!IsEditable)
        {
            return Error.Conflict("programme_locked", "The programme can no longer be edited once its quote is approved.");
        }

        if (name is not null)
        {
            Name = name.Trim();
        }

        Objectives = draft.Objectives ?? Objectives;
        CourseId = draft.CourseId ?? CourseId;
        OfferingId = draft.OfferingId ?? OfferingId;
        CourseTitle = draft.CourseTitle ?? CourseTitle;
        TutorUserId = draft.TutorUserId ?? TutorUserId;
        SeatCount = draft.SeatCount ?? SeatCount;
        TuitionPerSeat = draft.TuitionPerSeat ?? TuitionPerSeat;
        SponsorSharePercent = draft.SponsorSharePercent ?? SponsorSharePercent;
        Budget = draft.Budget ?? Budget;
        EligibilityRulesJson = draft.EligibilityRulesJson ?? EligibilityRulesJson;
        EntryAssessmentJson = draft.EntryAssessmentJson ?? EntryAssessmentJson;
        ScoringRubricJson = draft.ScoringRubricJson ?? ScoringRubricJson;
        ApplicationsOpenAt = draft.ApplicationsOpenAt ?? ApplicationsOpenAt;
        ApplicationsCloseAt = draft.ApplicationsCloseAt ?? ApplicationsCloseAt;
        StartsAt = draft.StartsAt ?? StartsAt;
        EndsAt = draft.EndsAt ?? EndsAt;
        SelectionMode = draft.SelectionMode ?? SelectionMode;
        UnusedFundsPolicy = draft.UnusedFundsPolicy ?? UnusedFundsPolicy;
        Status = ProgrammeStatus.Draft;
        QuotedAt = null;
        return Result.Success();
    }

    public Result<QuoteLines> RequestQuote(DateTime utcNow)
    {
        if (!IsEditable)
        {
            return Error.Conflict("programme_locked", "The quote is already approved.");
        }

        if (CourseId is null || OfferingId is null)
        {
            return Error.Validation("course_required", "Choose the course and offering before requesting a quote.");
        }

        if (SeatCount <= 0 || TuitionPerSeat.AmountKobo <= 0)
        {
            return Error.Validation("seats_required", "Seats and tuition per seat must be positive.");
        }

        if (StartsAt is null || EndsAt is null || EndsAt <= StartsAt)
        {
            return Error.Validation("dates_required", "Programme start and end dates are required.");
        }

        if (ApplicationsCloseAt is null || ApplicationsCloseAt > StartsAt)
        {
            return Error.Validation("applications_close_required", "Applications must close before the programme starts.");
        }

        var lines = QuoteLines.Compute(SeatCount, TuitionPerSeat, SponsorSharePercent);
        // The budget is compared before VAT (blueprint §5: ₦90m buys 500 seats; VAT is on top of the fees).
        var exVat = lines.Tuition + lines.ManagementFee + lines.SeatFee;
        if (!Budget.IsZero && exVat > Budget)
        {
            return Error.Validation("quote_exceeds_budget", $"The quote before VAT ({exVat}) exceeds the stated budget ({Budget}); reduce seats or raise the budget.");
        }

        QuoteVersion++;
        QuoteTuitionPerSeatShare = lines.TuitionPerSeatSponsorShare;
        QuoteTuition = lines.Tuition;
        QuoteManagementFee = lines.ManagementFee;
        QuoteSeatFee = lines.SeatFee;
        QuoteVat = lines.Vat;
        QuoteTotal = lines.Total;
        QuotedAt = utcNow;
        Status = ProgrammeStatus.Quoted;
        return lines;
    }

    public QuoteLines Quote => new(SeatCount, QuoteTuitionPerSeatShare, QuoteTuition, QuoteManagementFee, QuoteSeatFee, QuoteVat, QuoteTotal);

    public Result ApproveQuote(Guid userId, DateTime utcNow)
    {
        if (Status != ProgrammeStatus.Quoted)
        {
            return Error.Conflict("quote_not_pending", "There is no pending quote to approve.");
        }

        Status = ProgrammeStatus.QuoteApproved;
        QuoteApprovedAt = utcNow;
        QuoteApprovedBy = userId;
        return Result.Success();
    }

    public void AttachInvoice(Guid invoiceId) => InvoiceId = invoiceId;

    public Result Review(Guid staffId, ReviewStatus decision, bool rulesLawful, string? notes, string? paymentPlanJson, DateTime utcNow)
    {
        if (Status is not (ProgrammeStatus.Quoted or ProgrammeStatus.QuoteApproved))
        {
            return Error.Conflict("programme_not_reviewable", "Only quoted programmes awaiting publication are reviewed.");
        }

        if (decision == ReviewStatus.Approved && !rulesLawful)
        {
            return Error.Validation("rules_not_lawful", "A programme whose eligibility rules are not lawful and non-discriminatory cannot be approved.");
        }

        ReviewStatus = decision;
        ReviewedBy = staffId;
        ReviewedAt = utcNow;
        RulesLawful = rulesLawful;
        ReviewNotes = notes?.Trim();
        if (paymentPlanJson is not null)
        {
            PaymentPlanJson = paymentPlanJson;
            PaymentPlanApproved = decision == ReviewStatus.Approved;
        }

        return Result.Success();
    }

    public bool IsFunded => QuotedAt is not null && FundedAmount >= QuoteTotal;

    /// <summary>Records sponsor money received against the programme's invoice; true when this receipt completed the funding.</summary>
    public bool RecordFunding(Money amount, DateTime utcNow)
    {
        var wasFunded = IsFunded;
        FundedAmount += amount;
        if (!wasFunded && IsFunded)
        {
            FundedAt = utcNow;
            return true;
        }

        return false;
    }

    /// <summary>Seats ≤ funded amount ÷ seat cost (blueprint §9.12); an approved payment plan unlocks the full inventory.</summary>
    public int SeatsFundable => PaymentPlanApproved || IsFunded ? SeatCount : (int)Math.Min(SeatCount, Quote.TotalPerSeatKobo == 0 ? 0 : FundedAmount.AmountKobo / Quote.TotalPerSeatKobo);

    public Result<int> Publish(Guid userId, DateTime utcNow)
    {
        if (Status == ProgrammeStatus.Published)
        {
            return Error.Conflict("programme_already_published", "The programme is already published.");
        }

        if (Status != ProgrammeStatus.QuoteApproved)
        {
            return Error.Conflict("quote_not_approved", "The sponsor must approve the quote before publication.");
        }

        if (ReviewStatus != ReviewStatus.Approved)
        {
            return Error.Conflict("programme_not_reviewed", "Plastoids programme review has not approved this campaign.");
        }

        if (!IsFunded && !PaymentPlanApproved)
        {
            return Error.Conflict("programme_not_funded", "Publish only after the invoice is paid or a payment plan is approved.");
        }

        if (ApplicationsCloseAt is null || ApplicationsCloseAt <= utcNow)
        {
            return Error.Validation("applications_window_closed", "The application window must end in the future.");
        }

        Status = ProgrammeStatus.Published;
        PublishedAt = utcNow;
        PublishedBy = userId;
        ApplicationsOpenAt ??= utcNow;
        SeatsUnlocked = SeatsFundable;
        return SeatsUnlocked;
    }

    public bool IsAcceptingApplications(DateTime utcNow)
        => Status == ProgrammeStatus.Published && (ApplicationsOpenAt ?? PublishedAt) <= utcNow && ApplicationsCloseAt > utcNow;

    public int DurationMonths => StartsAt is { } s && EndsAt is { } e ? Math.Max(1, (int)Math.Round((e - s).TotalDays / 30.4375)) : 1;

    public Money CoPayPerSeat => TuitionPerSeat - TuitionPerSeat.Percent(SponsorSharePercent, MidpointRounding.ToEven);

    public Result<Money> Close(Guid userId, int unfilledSeats, DateTime utcNow)
    {
        if (Status != ProgrammeStatus.Published)
        {
            return Error.Conflict("programme_not_published", "Only published programmes can be closed.");
        }

        var refund = new Money(checked(Quote.RefundPerUnfilledSeat.AmountKobo * unfilledSeats), QuoteTotal.Currency);
        Status = ProgrammeStatus.Closed;
        ClosedAt = utcNow;
        ClosedBy = userId;
        CloseoutRefund = refund;
        return refund;
    }
}
