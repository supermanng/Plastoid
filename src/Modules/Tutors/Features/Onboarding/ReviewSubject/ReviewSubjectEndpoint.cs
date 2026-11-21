using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Tutors.Features.Onboarding.ReviewSubject;

internal static class ReviewSubjectEndpoint
{
    public static void Map(IEndpointRouteBuilder adminGroup)
    {
        adminGroup.MapPost("/{tutorId:guid}/subject-review", async (Guid tutorId, ReviewSubjectRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new ReviewSubjectCommand(tutorId, body.Decision, body.Score, body.Note, key), ct)).ToHttpResult();
            })
            .WithName("ReviewSubject")
            .WithSummary("Subject reviewer: approve or reject the demo lesson. Approval of the last stage verifies the tutor. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
