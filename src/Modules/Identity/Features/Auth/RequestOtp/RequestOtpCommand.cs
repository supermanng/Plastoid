using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Identity.Features.Auth.RequestOtp;

public sealed record RequestOtpCommand(string Phone) : ICommand<RequestOtpResponse>;

public sealed record RequestOtpResponse(string Phone, int ExpiresInSeconds);
