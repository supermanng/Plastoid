using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Identity.Features.Me.DeleteMyAccount;

/// <summary>Story A3: anonymises personal columns, revokes sessions, removes organisation memberships; ledger and learning rows stay.</summary>
public sealed record DeleteMyAccountCommand(string Confirmation) : ICommand;
