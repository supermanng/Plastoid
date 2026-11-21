using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Identity.Features.Me.GetMe;

public sealed record GetMeQuery : IQuery<MeResponse>;

public sealed record MeResponse(
    Guid Id,
    string PhoneMasked,
    string? Email,
    bool EmailVerified,
    string DisplayName,
    bool MfaEnabled,
    bool NinVerified,
    IReadOnlyList<string> Roles,
    IReadOnlyList<MeOrganisation> Organisations,
    string? CareerGoal,
    IReadOnlyList<string> Interests,
    DateTime CreatedAt);

public sealed record MeOrganisation(Guid Id, string Name, string Type, string Role, string VerificationStatus);
