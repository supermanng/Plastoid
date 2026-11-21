using Plastoids.SharedKernel.Messaging;

namespace Plastoids.Modules.Lms.Contracts;

/// <summary>lms.attendance-recorded.v1 (build prompt §2.8). EnrolmentId is null for 1:1 bookings (no enrolment); UserId identifies the attendee.</summary>
[IntegrationEvent("lms", "attendance-recorded", 1)]
public sealed record AttendanceRecorded(Guid AggregateId, Guid LiveSessionId, Guid? EnrolmentId, Guid UserId, Guid? BookingId, DateTime JoinedAt, DateTime LeftAt, int DurationSeconds) : IIntegrationEvent;
