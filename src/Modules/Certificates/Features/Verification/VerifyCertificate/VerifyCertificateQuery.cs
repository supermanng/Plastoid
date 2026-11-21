using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Certificates.Domain;
using Plastoids.Modules.Certificates.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Certificates.Features.Verification.VerifyCertificate;

/// <summary>Public verification page payload (also what the QR code encodes). Logs every lookup.</summary>
public sealed record VerifyCertificateQuery(string PublicCode) : IQuery<VerificationResult>;

public sealed record VerificationResult(string PublicCode, string Status, string? LearnerName, string? CourseTitle, DateTime? IssuedAt, decimal? FinalScore, string? Hash, bool HashMatches, DateTime? RevokedAt, string QrPayload);

internal sealed class VerifyCertificateHandler(CertificatesDbContext db, ICurrentUser currentUser, IClock clock) : IQueryHandler<VerifyCertificateQuery, VerificationResult>
{
    public async Task<Result<VerificationResult>> Handle(VerifyCertificateQuery request, CancellationToken cancellationToken)
    {
        var code = request.PublicCode.Trim().ToUpperInvariant();
        if (code.Length == 12)
        {
            code = $"{code[..4]}-{code[4..8]}-{code[8..]}";
        }

        var certificate = await db.Certificates.AsNoTracking().SingleOrDefaultAsync(c => c.PublicCode == code, cancellationToken);
        var outcome = certificate is null ? "unknown" : certificate.IsValid ? "valid" : "revoked";
        db.Verifications.Add(VerificationLog.Record(Tenants.Marketplace, code, outcome, currentUser.IpAddress, clock.UtcNow));
        await db.SaveChangesAsync(cancellationToken);

        var qr = $"https://plastoids.ng/verify/{code}";
        return certificate is null
            ? new VerificationResult(code, outcome, null, null, null, null, null, false, null, qr)
            : new VerificationResult(code, outcome, certificate.LearnerName, certificate.CourseTitle, certificate.IssuedAt, certificate.FinalScore, certificate.Hash, certificate.HashMatches, certificate.RevokedAt, qr);
    }
}

internal static class VerifyCertificateEndpoint
{
    public static void Map(IEndpointRouteBuilder v1)
    {
        v1.MapGet("/verify/{publicCode}", async (string publicCode, ISender sender, CancellationToken ct) => (await sender.Send(new VerifyCertificateQuery(publicCode), ct)).ToHttpResult())
            .WithName("VerifyCertificate")
            .WithSummary("Public: verify a certificate by its code (valid | revoked | unknown).")
            .AllowAnonymous();
    }
}
