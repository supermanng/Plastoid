using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Catalogue.Features.Authoring.CreateOffering;

public sealed record CreateOfferingCommand(Guid CourseId, string Format, Money Price, IReadOnlyList<string> FundingTypes, int? Capacity, DateTime? StartAt, int? DurationWeeks) : ICommand<CreateOfferingResponse>;

public sealed record CreateOfferingRequest(string Format, Money Price, IReadOnlyList<string> FundingTypes, int? Capacity, DateTime? StartAt, int? DurationWeeks);

public sealed record CreateOfferingResponse(Guid OfferingId, Guid CourseId, string Format, Money Price);
