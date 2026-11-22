using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Catalogue.Features.Authoring.CreateOffering;

/// <summary>TutorUserId: the engaged tutor who teaches this offering of an organisation course (required for cohort / one_to_one, ADR 0019); ignored on tutor courses.</summary>
public sealed record CreateOfferingCommand(Guid CourseId, string Format, Money Price, IReadOnlyList<string> FundingTypes, int? Capacity, DateTime? StartAt, int? DurationWeeks, Guid? TutorUserId = null) : ICommand<CreateOfferingResponse>;

public sealed record CreateOfferingRequest(string Format, Money Price, IReadOnlyList<string> FundingTypes, int? Capacity, DateTime? StartAt, int? DurationWeeks, Guid? TutorUserId = null);

public sealed record CreateOfferingResponse(Guid OfferingId, Guid CourseId, string Format, Money Price, Guid? TutorUserId = null, Guid? EngagementId = null);
