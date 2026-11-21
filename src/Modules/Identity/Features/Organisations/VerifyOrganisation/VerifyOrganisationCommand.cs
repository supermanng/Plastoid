using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Identity.Features.Organisations.VerifyOrganisation;

/// <summary>Staff decision on an organisation. A decision write → Idempotency-Key required (build prompt §2.11).</summary>
public sealed record VerifyOrganisationCommand(Guid OrganisationId, string Decision, string? Reason, string IdempotencyKey)
    : ICommand<VerifyOrganisationResponse>, IIdempotentCommand;

public sealed record VerifyOrganisationRequest(string Decision, string? Reason);

public sealed record VerifyOrganisationResponse(Guid OrganisationId, string VerificationStatus);

public static class VerificationDecisions
{
    public const string Verified = "verified";
    public const string Rejected = "rejected";
}
