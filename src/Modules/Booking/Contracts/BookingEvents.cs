using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Booking.Contracts;

/// <summary>booking.booking-confirmed.v1 (build prompt §2.8): payment captured (or package session used). Lms creates the live session.</summary>
[IntegrationEvent("booking", "booking-confirmed", 1)]
public sealed record BookingConfirmed(Guid AggregateId, Guid BookingId, Guid TutorId, Guid TutorUserId, Guid LearnerId, DateTime SlotStart, DateTime SlotEnd, string Format) : IIntegrationEvent;

/// <summary>booking.booking-cancelled.v1: cancelledBy is learner | tutor | system; refundPercent 0–100.</summary>
[IntegrationEvent("booking", "booking-cancelled", 1)]
public sealed record BookingCancelled(Guid AggregateId, Guid BookingId, string CancelledBy, int RefundPercent, bool TutorNoShow) : IIntegrationEvent;

/// <summary>booking.booking-rescheduled.v1: Lms moves the live session.</summary>
[IntegrationEvent("booking", "booking-rescheduled", 1)]
public sealed record BookingRescheduled(Guid AggregateId, Guid BookingId, DateTime SlotStart, DateTime SlotEnd) : IIntegrationEvent;

/// <summary>booking.earning-released.v1: the 72-hour hold passed; Payouts (Sprint 7) adds it to the next run.</summary>
[IntegrationEvent("booking", "earning-released", 1)]
public sealed record EarningReleased(Guid AggregateId, Guid HoldId, Guid BookingId, Guid TutorUserId, Money Amount) : IIntegrationEvent;

/// <summary>booking.tutor-strike-recorded.v1: three strikes within 90 days flag the tutor for review (blueprint §9).</summary>
[IntegrationEvent("booking", "tutor-strike-recorded", 1)]
public sealed record TutorStrikeRecorded(Guid AggregateId, Guid TutorId, Guid BookingId, int StrikesInLast90Days, bool ReviewFlagged) : IIntegrationEvent;
