using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Applications.Domain;
using Plastoids.Modules.Applications.Persistence;
using Plastoids.Modules.Applications.Services;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Applications.Features.Review.ScoreApplication;

/// <summary>Reviewer scores the rubric criteria; the total combines them with the entry assessment. Answers never enter the scorer.</summary>
public sealed record ScoreApplicationCommand(Guid ApplicationId, IReadOnlyDictionary<string, decimal> Scores) : ICommand<ScoreApplicationResponse>;

public sealed record ScoreApplicationRequest(IReadOnlyDictionary<string, decimal> Scores);

public sealed record ScoreApplicationResponse(Guid ApplicationId, decimal? AssessmentScore, decimal? RubricScore, decimal TotalScore);

internal sealed class ScoreApplicationValidator : AbstractValidator<ScoreApplicationCommand>
{
    public ScoreApplicationValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.Scores).NotNull().Must(s => s.Count <= 20);
        RuleFor(x => x.Scores).Must(s => !s.Keys.Any(AnswerFields.ProtectedCharacteristics.Contains)).WithMessage("Protected characteristics cannot be scored.");
    }
}

internal sealed class ScoreApplicationHandler(ApplicationsDbContext db, IProgrammeReader programmes, ICurrentUser currentUser, IClock clock) : ICommandHandler<ScoreApplicationCommand, ScoreApplicationResponse>
{
    public async Task<Result<ScoreApplicationResponse>> Handle(ScoreApplicationCommand request, CancellationToken cancellationToken)
    {
        var application = await db.Applications.SingleOrDefaultAsync(a => a.Id == request.ApplicationId, cancellationToken);
        if (application is null || application.Status == ApplicationStatus.Draft)
        {
            return Error.NotFound("application_not_found", "No submitted application with that id.");
        }

        var programme = await programmes.GetAsync(application.ProgrammeId, cancellationToken);
        var rubric = RubricParser.Parse(programme?.ScoringRubric);
        var outcome = Scorer.Compute(new ScoringInput(application.AssessmentScore, request.Scores), rubric);
        if (outcome.IsFailure)
        {
            return Result.Failure<ScoreApplicationResponse>(outcome.Error);
        }

        application.RecordScores(outcome.Value.RubricPercent, outcome.Value.TotalPercent, currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        return new ScoreApplicationResponse(application.Id, application.AssessmentScore, application.RubricScore, application.TotalScore!.Value);
    }
}

internal static class ScoreApplicationEndpoint
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapPost("/{applicationId:guid}/score", async (Guid applicationId, ScoreApplicationRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new ScoreApplicationCommand(applicationId, body.Scores), ct)).ToHttpResult())
            .WithName("ScoreApplication")
            .WithSummary("Staff: score the rubric criteria { key: score }; total = assessment × weight + rubric × (1 − weight).")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
