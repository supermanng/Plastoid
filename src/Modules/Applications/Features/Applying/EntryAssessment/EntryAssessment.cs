using System.Text.Json;
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
using Plastoids.Modules.Lms.Contracts;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Applications.Features.Applying.EntryAssessment;

/// <summary>Story F3: timed entry assessment reusing the Lms engine (Lms.Contracts). One attempt per application; timing is server-side.</summary>
public sealed record StartEntryAssessmentCommand(Guid ApplicationId) : ICommand<EntryAssessmentView>;

public sealed record SubmitEntryAssessmentCommand(Guid ApplicationId, JsonElement Answers) : ICommand<EntryAssessmentResult>;

public sealed record SubmitEntryAssessmentRequest(JsonElement Answers);

public sealed record EntryAssessmentView(Guid AttemptId, Guid ApplicationId, DateTime StartedAt, DateTime DeadlineAt, int SecondsRemaining, JsonElement Questions);

public sealed record EntryAssessmentResult(Guid AttemptId, string Status, decimal Score, decimal MaxScore, bool Passed, int Correct, int Total);

internal sealed class StartEntryAssessmentValidator : AbstractValidator<StartEntryAssessmentCommand>
{
    public StartEntryAssessmentValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
    }
}

internal sealed class SubmitEntryAssessmentValidator : AbstractValidator<SubmitEntryAssessmentCommand>
{
    public SubmitEntryAssessmentValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.Answers).Must(a => a.ValueKind == JsonValueKind.Object).WithMessage("answers must be an object keyed by question index.");
    }
}

internal sealed class StartEntryAssessmentHandler(ApplicationsDbContext db, ApplicationAccess access, IProgrammeReader programmes, IAssessmentGrader grader, IClock clock) : ICommandHandler<StartEntryAssessmentCommand, EntryAssessmentView>
{
    public async Task<Result<EntryAssessmentView>> Handle(StartEntryAssessmentCommand request, CancellationToken cancellationToken)
    {
        var mine = await access.MineAsync(request.ApplicationId, cancellationToken);
        if (mine.IsFailure)
        {
            return Result.Failure<EntryAssessmentView>(mine.Error);
        }

        var application = mine.Value;
        if (application.Status != ApplicationStatus.Submitted)
        {
            return Error.Conflict("application_not_submitted", "Submit the application before taking the assessment.");
        }

        var programme = await programmes.GetAsync(application.ProgrammeId, cancellationToken);
        if (programme?.EntryAssessment is not { } quiz)
        {
            return Error.NotFound("no_entry_assessment", "This programme has no entry assessment.");
        }

        var now = clock.UtcNow;
        var existing = await db.Attempts.Where(a => a.ApplicationId == application.Id).OrderByDescending(a => a.StartedAt).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null && (existing.Status != EntryAttemptStatus.InProgress || now > existing.DeadlineAt))
        {
            return Error.Conflict("assessment_already_taken", "The entry assessment allows one attempt.");
        }

        var attempt = existing;
        if (attempt is null)
        {
            attempt = EntryAttempt.Start(application.TenantId, application.Id, grader.TimeLimitMinutes(quiz), now);
            db.Attempts.Add(attempt);
        }

        return new EntryAssessmentView(attempt.Id, application.Id, attempt.StartedAt, attempt.DeadlineAt, (int)Math.Max(0, (attempt.DeadlineAt - now).TotalSeconds), grader.Redact(quiz));
    }
}

internal sealed class SubmitEntryAssessmentHandler(ApplicationsDbContext db, ApplicationAccess access, IProgrammeReader programmes, IAssessmentGrader grader, IClock clock) : ICommandHandler<SubmitEntryAssessmentCommand, EntryAssessmentResult>
{
    public async Task<Result<EntryAssessmentResult>> Handle(SubmitEntryAssessmentCommand request, CancellationToken cancellationToken)
    {
        var mine = await access.MineAsync(request.ApplicationId, cancellationToken);
        if (mine.IsFailure)
        {
            return Result.Failure<EntryAssessmentResult>(mine.Error);
        }

        var application = mine.Value;
        var attempt = await db.Attempts.Where(a => a.ApplicationId == application.Id && a.Status == EntryAttemptStatus.InProgress).OrderByDescending(a => a.StartedAt).FirstOrDefaultAsync(cancellationToken);
        if (attempt is null)
        {
            return Error.NotFound("assessment_not_started", "Start the assessment first.");
        }

        var programme = await programmes.GetAsync(application.ProgrammeId, cancellationToken);
        if (programme?.EntryAssessment is not { } quiz)
        {
            return Error.NotFound("no_entry_assessment", "This programme has no entry assessment.");
        }

        var grade = grader.Grade(quiz, request.Answers);
        var submitted = attempt.Submit(request.Answers.GetRawText(), grade.Score, grade.MaxScore, grade.Passed, clock.UtcNow);
        if (submitted.IsFailure)
        {
            return Result.Failure<EntryAssessmentResult>(submitted.Error);
        }

        application.RecordAssessment(attempt.Score, attempt.Passed);
        return new EntryAssessmentResult(attempt.Id, attempt.Status.ToString().ToLowerInvariant(), attempt.Score, attempt.MaxScore, attempt.Passed, grade.Correct, grade.Total);
    }
}

internal static class EntryAssessmentEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{applicationId:guid}/assessment", async (Guid applicationId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new StartEntryAssessmentCommand(applicationId), ct)).ToHttpResult(StatusCodes.Status201Created))
            .WithName("StartEntryAssessment")
            .WithSummary("Learner: start the timed entry assessment (questions without answers; deadline is server-side).")
            .RequireAuthorization(Policies.Authenticated);

        group.MapPut("/{applicationId:guid}/assessment", async (Guid applicationId, SubmitEntryAssessmentRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SubmitEntryAssessmentCommand(applicationId, body.Answers), ct)).ToHttpResult())
            .WithName("SubmitEntryAssessment")
            .WithSummary("Learner: submit answers; graded by the Lms engine. After the deadline (+30 s) the attempt is expired and cannot pass.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
