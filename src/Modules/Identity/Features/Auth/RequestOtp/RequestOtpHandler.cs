using Plastoids.Modules.Identity.Domain;
using Plastoids.Modules.Identity.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Identity.Features.Auth.RequestOtp;

internal sealed class RequestOtpHandler(OtpService otp) : ICommandHandler<RequestOtpCommand, RequestOtpResponse>
{
    public async Task<Result<RequestOtpResponse>> Handle(RequestOtpCommand request, CancellationToken cancellationToken)
    {
        PhoneNumber.TryNormalise(request.Phone, out var phone);
        var result = await otp.RequestAsync(phone, cancellationToken);
        return result.IsFailure
            ? Result.Failure<RequestOtpResponse>(result.Error)
            : new RequestOtpResponse(PhoneNumber.Mask(phone), (int)result.Value.TotalSeconds);
    }
}
