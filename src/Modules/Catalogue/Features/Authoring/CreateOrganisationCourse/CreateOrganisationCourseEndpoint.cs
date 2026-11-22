using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Identity.Authorization;

namespace Plastoids.Modules.Catalogue.Features.Authoring.CreateOrganisationCourse;

internal static class CreateOrganisationCourseEndpoint
{
    public static void Map(IEndpointRouteBuilder organisations)
    {
        organisations.MapPost("/{organisationId:guid}/courses", async (Guid organisationId, CreateOrganisationCourseRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateOrganisationCourseCommand(organisationId, body.Title, body.Summary, body.Level, body.Language, body.Category, body.Skills), ct))
                    .ToCreatedResult(r => $"/api/v1/catalogue/courses/{r.CourseId}/authoring"))
            .WithName("CreateOrganisationCourse")
            .WithSummary("Organisation owner/author: start a draft course owned by the (verified) organisation.")
            .RequireAuthorization(IdentityPolicies.OrganisationAuthor);
    }
}
