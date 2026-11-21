using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Tutors.Features.Profile.SetPricing;

public sealed record PriceRuleInput(string Format, int DurationMinutes, Money Price, int? MaxLearners);

public sealed record SetPricingCommand(Money HourlyRate, IReadOnlyList<PriceRuleInput> Rules) : ICommand;
