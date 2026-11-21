using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Identity.Features.Auth.EnableMfa;

/// <summary>Starts TOTP enrolment for the caller. MFA is enforced only after VerifyMfa confirms a code.</summary>
public sealed record EnableMfaCommand : ICommand<EnableMfaResponse>;

public sealed record EnableMfaResponse(string Secret, string OtpAuthUri, IReadOnlyList<string> RecoveryCodes);
