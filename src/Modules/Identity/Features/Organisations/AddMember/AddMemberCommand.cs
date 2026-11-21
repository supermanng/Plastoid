using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Identity.Features.Organisations.AddMember;

/// <summary>Owner adds an existing Plastoids user (by email or phone) to the organisation with a role.</summary>
public sealed record AddMemberCommand(Guid OrganisationId, string? Email, string? Phone, string Role) : ICommand<AddMemberResponse>;

public sealed record AddMemberRequest(string? Email, string? Phone, string Role);

public sealed record AddMemberResponse(Guid OrganisationId, Guid UserId, string Role);
