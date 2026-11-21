using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Sponsorship.Domain;

public enum SeatStatus
{
    Open = 0,
    Reserved = 1,
    Filled = 2,
    Refunded = 3,
}

/// <summary>Seat inventory (blueprint §9.12). Reserved by an offer, filled on acceptance, re-opened on release, refunded at close-out.</summary>
public sealed class Seat : Entity
{
    private Seat()
    {
    }

    private Seat(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid ProgrammeId { get; private set; }
    public int SeatNo { get; private set; }
    public SeatStatus Status { get; private set; }
    public Guid? ApplicationId { get; private set; }
    public Guid? LearnerId { get; private set; }
    public Guid? EnrolmentId { get; private set; }
    public DateTime? ReservedAt { get; private set; }
    public DateTime? FilledAt { get; private set; }
    public DateTime? ReleasedAt { get; private set; }
    public string? ReleaseReason { get; private set; }
    public int FillCount { get; private set; }
    public int AttendanceCount { get; private set; }
    public decimal? LastScore { get; private set; }

    public static Seat Open(Guid tenantId, Guid programmeId, int seatNo) => new(NewId(), tenantId) { ProgrammeId = programmeId, SeatNo = seatNo, Status = SeatStatus.Open };

    public Result Reserve(Guid applicationId, Guid learnerId, DateTime utcNow)
    {
        if (Status != SeatStatus.Open)
        {
            return Error.Conflict("seat_not_open", "The seat is not open.");
        }

        Status = SeatStatus.Reserved;
        ApplicationId = applicationId;
        LearnerId = learnerId;
        ReservedAt = utcNow;
        return Result.Success();
    }

    public Result Fill(Guid applicationId, Guid learnerId, DateTime utcNow)
    {
        if (Status == SeatStatus.Filled && ApplicationId == applicationId)
        {
            return Result.Success();
        }

        if (Status is not (SeatStatus.Reserved or SeatStatus.Open))
        {
            return Error.Conflict("seat_not_available", "The seat cannot be filled.");
        }

        Status = SeatStatus.Filled;
        ApplicationId = applicationId;
        LearnerId = learnerId;
        FilledAt = utcNow;
        FillCount++;
        return Result.Success();
    }

    /// <summary>Back to the pool: a declined/expired offer or a drop-out before week 3 (blueprint §10: replacement is free).</summary>
    public Result Release(string reason, DateTime utcNow)
    {
        if (Status is not (SeatStatus.Reserved or SeatStatus.Filled))
        {
            return Error.Conflict("seat_not_held", "The seat is not reserved or filled.");
        }

        Status = SeatStatus.Open;
        ApplicationId = null;
        LearnerId = null;
        EnrolmentId = null;
        AttendanceCount = 0;
        LastScore = null;
        ReleasedAt = utcNow;
        ReleaseReason = reason.Trim();
        return Result.Success();
    }

    public void Refund()
    {
        if (Status == SeatStatus.Open)
        {
            Status = SeatStatus.Refunded;
        }
    }

    public void LinkEnrolment(Guid enrolmentId) => EnrolmentId = enrolmentId;
    public void RecordAttendance() => AttendanceCount++;
    public void RecordScore(decimal score) => LastScore = score;
}

/// <summary>Event-derived funnel (story F5): applied → offered → enrolled → active → completed → certified.</summary>
public sealed class ProgrammeFunnel : Entity
{
    private ProgrammeFunnel()
    {
    }

    private ProgrammeFunnel(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid ProgrammeId { get; private set; }
    public int Applied { get; private set; }
    public int Offered { get; private set; }
    public int Enrolled { get; private set; }
    public int Active { get; private set; }
    public int Completed { get; private set; }
    public int Certified { get; private set; }
    public DateTime RefreshedAt { get; private set; }

    public static ProgrammeFunnel Start(Guid tenantId, Guid programmeId, DateTime utcNow) => new(NewId(), tenantId) { ProgrammeId = programmeId, RefreshedAt = utcNow };

    public void Bump(string stage, DateTime utcNow)
    {
        switch (stage)
        {
            case "applied": Applied++; break;
            case "offered": Offered++; break;
            case "enrolled": Enrolled++; break;
            case "active": Active++; break;
            case "completed": Completed++; break;
            case "certified": Certified++; break;
            default: throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unknown funnel stage.");
        }

        RefreshedAt = utcNow;
    }
}

/// <summary>One programme month's entries 8 and 9; unique per (programme, month) so the job is idempotent.</summary>
public sealed class ProgrammeMonth : Entity
{
    private ProgrammeMonth()
    {
    }

    private ProgrammeMonth(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid ProgrammeId { get; private set; }
    public int MonthIndex { get; private set; }
    public int FilledSeats { get; private set; }
    public Money ManagementFee { get; private set; }
    public Money TutorEarnings { get; private set; }
    public DateTime PostedAt { get; private set; }

    public static ProgrammeMonth Record(Guid tenantId, Guid programmeId, int monthIndex, int filledSeats, Money managementFee, Money tutorEarnings, DateTime utcNow)
        => new(NewId(), tenantId) { ProgrammeId = programmeId, MonthIndex = monthIndex, FilledSeats = filledSeats, ManagementFee = managementFee, TutorEarnings = tutorEarnings, PostedAt = utcNow };
}

/// <summary>Attendance facts copied from lms.attendance-recorded.v1 for the impact report.</summary>
public sealed class ProgrammeAttendance : Entity
{
    private ProgrammeAttendance()
    {
    }

    private ProgrammeAttendance(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid ProgrammeId { get; private set; }
    public Guid SeatId { get; private set; }
    public Guid EnrolmentId { get; private set; }
    public Guid LiveSessionId { get; private set; }
    public DateTime JoinedAt { get; private set; }
    public DateTime? LeftAt { get; private set; }

    public static ProgrammeAttendance Record(Guid tenantId, Guid programmeId, Guid seatId, Guid enrolmentId, Guid liveSessionId, DateTime joinedAt, DateTime? leftAt)
        => new(NewId(), tenantId) { ProgrammeId = programmeId, SeatId = seatId, EnrolmentId = enrolmentId, LiveSessionId = liveSessionId, JoinedAt = joinedAt, LeftAt = leftAt };
}

/// <summary>Money received against the programme (invoice paid or tranche), for the budget view.</summary>
public sealed class FundingReceipt : Entity
{
    private FundingReceipt()
    {
    }

    private FundingReceipt(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid ProgrammeId { get; private set; }
    public Guid InvoiceId { get; private set; }
    public Money Amount { get; private set; }
    public DateTime ReceivedAt { get; private set; }

    public static FundingReceipt Record(Guid tenantId, Guid programmeId, Guid invoiceId, Money amount, DateTime receivedAt)
        => new(NewId(), tenantId) { ProgrammeId = programmeId, InvoiceId = invoiceId, Amount = amount, ReceivedAt = receivedAt };
}
