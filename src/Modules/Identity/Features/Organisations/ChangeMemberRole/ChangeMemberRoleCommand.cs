using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Identity.Features.Organisations.ChangeMemberRole;

public sealed record ChangeMemberRoleCommand(Guid OrganisationId, Guid UserId, string Role) : ICommand;

public sealed record ChangeMemberRoleRequest(string Role);
