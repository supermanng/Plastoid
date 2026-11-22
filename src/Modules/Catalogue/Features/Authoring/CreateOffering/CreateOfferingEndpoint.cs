using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Catalogue.Features.Authoring.CreateOffering;

internal static class CreateOfferingEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{courseId:guid}/offerings", async (Guid courseId, CreateOfferingRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateOfferingCommand(courseId, body.Format, body.Price, body.FundingTypes, body.Capacity, body.StartAt, body.DurationWeeks, body.TutorUserId), ct))
                    .ToCreatedResult(r => $"/api/v1/catalogue/courses/{r.CourseId}/offerings/{r.OfferingId}"))
            .WithName("CreateOffering")
            .WithSummary("Owner: sell the course as self_paced | cohort | one_to_one with a kobo price and accepted funding types. Organisation courses name the engaged tutor (tutorUserId) for cohort and one_to_one offerings.")
            .RequireAuthorization(Policies.Mfa); // ownership (tutor, organisation owner/author, staff) is decided by CourseAccess (ADR 0019 §2)
    }
}
