using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Tutors.Features.Onboarding.ApplyAsTutor;

internal static class ApplyAsTutorEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/applications", async (ApplyAsTutorCommand command, ISender sender, CancellationToken ct) =>
                (await sender.Send(command, ct)).ToCreatedResult(r => "/api/v1/tutors/me"))
            .WithName("ApplyAsTutor")
            .WithSummary("Apply to teach: profile, subjects, formats, hourly rate, code of conduct. Grants the tutor role; refresh your token afterwards.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
