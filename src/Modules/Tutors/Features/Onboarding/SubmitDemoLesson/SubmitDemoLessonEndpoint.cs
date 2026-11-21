using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Tutors.Features.Onboarding.SubmitDemoLesson;

internal static class SubmitDemoLessonEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/demo-lesson", async (SubmitDemoLessonCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToHttpResult(StatusCodes.Status202Accepted))
            .WithName("SubmitDemoLesson")
            .WithSummary("Stage 3: upload the 5-minute demo lesson; queued for the subject reviewer panel (5 working days).")
            .RequireAuthorization(Policies.Tutor);
    }
}
