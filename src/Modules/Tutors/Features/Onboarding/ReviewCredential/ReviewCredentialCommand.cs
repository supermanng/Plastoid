using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Tutors.Features.Onboarding.ReviewCredential;

public sealed record ReviewCredentialCommand(Guid CredentialId, string Decision, string? Note, string IdempotencyKey) : ICommand<ReviewCredentialResponse>, IIdempotentCommand;

public sealed record ReviewCredentialRequest(string Decision, string? Note);

public sealed record ReviewCredentialResponse(Guid CredentialId, string Status, bool QualificationsStagePassed);

public static class ReviewDecisions
{
    public const string Approved = "approved";
    public const string Rejected = "rejected";
}
