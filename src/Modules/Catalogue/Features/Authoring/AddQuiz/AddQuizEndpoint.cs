using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;

namespace Plastoids.Modules.Catalogue.Features.Authoring.AddQuiz;

internal static class AddQuizEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{courseId:guid}/modules/{moduleId:guid}/quizzes", async (Guid courseId, Guid moduleId, AddQuizRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new AddQuizCommand(courseId, moduleId, body.Title, body.Order, body.PassMarkPercent, body.Questions), ct))
                    .ToCreatedResult(r => $"/api/v1/catalogue/lessons/{r.LessonId}"))
            .WithName("AddQuiz")
            .WithSummary("Owner: add a multiple-choice quiz (questions, options, correct index, pass mark).")
            .RequireAuthorization(Policies.Tutor);
    }
}
