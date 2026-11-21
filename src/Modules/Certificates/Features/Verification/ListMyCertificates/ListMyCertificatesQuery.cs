using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Certificates.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Certificates.Features.Verification.ListMyCertificates;

public sealed record ListMyCertificatesQuery : IQuery<IReadOnlyList<CertificateView>>;

public sealed record CertificateView(Guid CertificateId, Guid EnrolmentId, Guid CourseId, string CourseTitle, string LearnerName, string PublicCode, string Hash, decimal FinalScore, DateTime IssuedAt, DateTime? RevokedAt, string VerifyUrl);

internal sealed class ListMyCertificatesHandler(CertificatesDbContext db, ICurrentUser currentUser) : IQueryHandler<ListMyCertificatesQuery, IReadOnlyList<CertificateView>>
{
    public async Task<Result<IReadOnlyList<CertificateView>>> Handle(ListMyCertificatesQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId ?? Guid.Empty;
        var rows = await db.Certificates.AsNoTracking().Where(c => c.LearnerId == userId).OrderByDescending(c => c.IssuedAt).ToListAsync(cancellationToken);
        return rows.Select(c => new CertificateView(c.Id, c.EnrolmentId, c.CourseId, c.CourseTitle, c.LearnerName, c.PublicCode, c.Hash, c.FinalScore, c.IssuedAt, c.RevokedAt, $"https://plastoids.ng/verify/{c.PublicCode}")).ToList();
    }
}

internal static class ListMyCertificatesEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/me", async (ISender sender, CancellationToken ct) => (await sender.Send(new ListMyCertificatesQuery(), ct)).ToHttpResult())
            .WithName("ListMyCertificates")
            .RequireAuthorization(Policies.Authenticated);
    }
}
