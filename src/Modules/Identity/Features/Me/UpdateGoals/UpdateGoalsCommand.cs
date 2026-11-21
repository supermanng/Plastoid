using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Identity.Features.Me.UpdateGoals;

public sealed record UpdateGoalsCommand(string? CareerGoal, IReadOnlyList<string> Interests) : ICommand;
