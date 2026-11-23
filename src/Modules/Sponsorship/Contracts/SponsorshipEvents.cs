using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Sponsorship.Contracts;

/// <summary>sponsorship.programme-funded.v1 (build prompt §2.8): the invoice was paid (or a payment plan approved); seats are unlocked.</summary>
[IntegrationEvent("sponsorship", "programme-funded", 1)]
public sealed record ProgrammeFunded(Guid AggregateId, Guid ProgrammeId, int SeatsFunded) : IIntegrationEvent;

/// <summary>sponsorship.programme-published.v1: the opportunity is live and accepting applications.</summary>
[IntegrationEvent("sponsorship", "programme-published", 1)]
public sealed record ProgrammePublished(Guid AggregateId, Guid ProgrammeId, Guid OrganisationId, string Name, DateTime ApplicationsCloseAt) : IIntegrationEvent;

/// <summary>sponsorship.seat-filled.v1: an accepted offer filled a seat; the seat fee is earned (ledger entry 7).</summary>
[IntegrationEvent("sponsorship", "seat-filled", 1)]
public sealed record SeatFilled(Guid AggregateId, Guid ProgrammeId, Guid SeatId, Guid ApplicationId, Guid LearnerId) : IIntegrationEvent;

/// <summary>sponsorship.seat-released.v1: a filled seat was given up; Applications backfills it from the waitlist.</summary>
[IntegrationEvent("sponsorship", "seat-released", 1)]
public sealed record SeatReleased(Guid AggregateId, Guid ProgrammeId, Guid SeatId, string Reason) : IIntegrationEvent;

/// <summary>sponsorship.tutor-earnings-accrued.v1: entry 9 for a programme month was posted; Payouts treats it as payable (delivered teaching, blueprint §18).</summary>
[IntegrationEvent("sponsorship", "tutor-earnings-accrued", 1)]
public sealed record TutorEarningsAccrued(Guid AggregateId, Guid ProgrammeId, Guid TutorUserId, int MonthIndex, Money Amount) : IIntegrationEvent;

/// <summary>
/// sponsorship.seat-allocated.v1 (ADR 0021): a sponsor gave a paid cohort seat to a learner and the seat's tuition was posted (entry 15).
/// Lms enrols the learner in the cohort (funding sponsored, FundingSourceId = BlockId, label = SponsorName); Payouts records the
/// earnings (PayeeType "tutor" | "organisation" is paid PayeeNet; TutorUserId, when set with a non-zero TutorShare, is paid that share);
/// Notifications tells the learner.
/// </summary>
[IntegrationEvent("sponsorship", "seat-allocated", 1)]
public sealed record SeatAllocated(
    Guid AggregateId,
    Guid BlockId,
    Guid AllocationId,
    Guid OrganisationId,
    string SponsorName,
    Guid CohortId,
    string CohortName,
    Guid CourseId,
    string CourseTitle,
    Guid OfferingId,
    Guid LearnerId,
    Money Price,
    string PayeeType,
    Guid PayeeId,
    Money PayeeNet,
    Guid? TutorUserId,
    Money TutorShare,
    Money Commission) : IIntegrationEvent;
