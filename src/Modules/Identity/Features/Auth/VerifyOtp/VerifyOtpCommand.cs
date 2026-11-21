using Plastoids.Modules.Identity.Services;
using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Identity.Features.Auth.VerifyOtp;

/// <summary>Verifies the code. A new phone creates the account, which needs DateOfBirth (18+) and DisplayName.</summary>
public sealed record VerifyOtpCommand(string Phone, string Code, DateOnly? DateOfBirth, string? DisplayName) : ICommand<VerifyOtpResponse>;

public sealed record VerifyOtpResponse(bool IsNewAccount, Guid UserId, TokenResponse Tokens);
