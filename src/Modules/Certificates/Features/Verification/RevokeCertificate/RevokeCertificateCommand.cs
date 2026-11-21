using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Certificates.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Certificates.Features.Verification.RevokeCertificate;

public sealed record RevokeCertificateCommand(Guid CertificateId, string Reason, string IdempotencyKey) : ICommand<RevokeCertificateResponse>, IIdempotentCommand;

public sealed record RevokeCertificateRequest(string Reason);

public sealed record RevokeCertificateResponse(Guid CertificateId, string PublicCode, DateTime RevokedAt);

internal sealed class RevokeCertificateValidator : AbstractValidator<RevokeCertificateCommand>
{
    public RevokeCertificateValidator()
    {
        RuleFor(x => x.CertificateId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(5).MaximumLength(500);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class RevokeCertificateHandler(CertificatesDbContext db, ICurrentUser currentUser, IClock clock) : ICommandHandler<RevokeCertificateCommand, RevokeCertificateResponse>
{
    public async Task<Result<RevokeCertificateResponse>> Handle(RevokeCertificateCommand request, CancellationToken cancellationToken)
    {
        var certificate = await db.Certificates.SingleOrDefaultAsync(c => c.Id == request.CertificateId, cancellationToken);
        if (certificate is null)
        {
            return Error.NotFound("certificate_not_found", "No such certificate.");
        }

        var revoked = certificate.Revoke(currentUser.UserId ?? Guid.Empty, request.Reason, clock.UtcNow);
        if (revoked.IsFailure)
        {
            return Result.Failure<RevokeCertificateResponse>(revoked.Error);
        }

        return new RevokeCertificateResponse(certificate.Id, certificate.PublicCode, certificate.RevokedAt!.Value);
    }
}

internal static class RevokeCertificateEndpoint
{
    public static void Map(IEndpointRouteBuilder adminGroup)
    {
        adminGroup.MapPost("/{certificateId:guid}/revoke", async (Guid certificateId, RevokeCertificateRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new RevokeCertificateCommand(certificateId, body.Reason, key), ct)).ToHttpResult();
            })
            .WithName("RevokeCertificate")
            .WithSummary("Staff: revoke a certificate (record kept; verification page shows revoked). Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
