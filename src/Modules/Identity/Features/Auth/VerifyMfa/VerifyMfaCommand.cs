using Plastoids.Modules.Identity.Services;
using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Identity.Features.Auth.VerifyMfa;

/// <summary>
/// Two uses: (a) signed-in user confirms enrolment with a code → MFA becomes enabled, new tokens carry amr=mfa;
/// (b) anonymous caller completes a login challenge with MfaToken + code (or a recovery code) → full tokens.
/// </summary>
public sealed record VerifyMfaCommand(string Code, string? MfaToken) : ICommand<TokenResponse>;
