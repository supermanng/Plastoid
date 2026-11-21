using Plastoids.Modules.Tutors.Domain;
using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Tutors.Features.Discovery.GetTutorAvailability;

public sealed record GetTutorAvailabilityQuery(Guid TutorId, DateOnly? From, int? Days) : IQuery<TutorAvailabilityResponse>;

public sealed record TutorAvailabilityResponse(Guid TutorId, string TimeZone, DateOnly From, int Days, IReadOnlyList<Slot> Slots);
