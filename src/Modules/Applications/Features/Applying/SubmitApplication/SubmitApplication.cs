using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Applications.Contracts;
using Plastoids.Modules.Applications.Domain;
using Plastoids.Modules.Applications.Persistence;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Applications.Features.Applying.SubmitApplication;

/// <summary>Story F3: ≤ 8 fields, save-and-resume (submit=false keeps a draft), one application per learner per programme; duplicates by NIN/phone are merged and flagged.</summary>
public sealed record SubmitApplicationCommand(Guid ProgrammeId, JsonElement Answers, bool Submit) : ICommand<ApplicationView>;

public sealed record SubmitApplicationRequest(JsonElement Answers, bool? Submit);

public sealed record ListMyApplicationsQuery : IQuery<IReadOnlyList<ApplicationView>>;

public sealed record ApplicationView(Guid ApplicationId, Guid ProgrammeId, string Status, DateTime? SubmittedAt, bool Flagged, string? FlagReason, bool? Eligible, IReadOnlyList<string> EligibilityReasons, decimal? AssessmentScore, bool? AssessmentPassed, decimal? TotalScore, Guid? SeatId, DateTime? OfferExpiresAt, Money CoPayAmount, JsonElement Answers)
{
    public static ApplicationView From(Application a, bool? eligible = null, IReadOnlyList<string>? reasons = null)
    {
        using var answers = JsonDocument.Parse(a.AnswersJson);
        return new ApplicationView(a.Id, a.ProgrammeId, a.Status.ToString().ToLowerInvariant(), a.SubmittedAt, a.IsFlagged, a.FlagReason, eligible, reasons ?? [], a.AssessmentScore, a.AssessmentPassed, a.TotalScore, a.SeatId, a.OfferExpiresAt, a.CoPayAmount, answers.RootElement.Clone());
    }
}

internal sealed class SubmitApplicationValidator : AbstractValidator<SubmitApplicationCommand>
{
    public SubmitApplicationValidator()
    {
        RuleFor(x => x.ProgrammeId).NotEmpty();
        RuleFor(x => x.Answers).Must(a => AnswerFields.Validate(a).IsSuccess).WithMessage(x => AnswerFields.Validate(x.Answers).Error.Message);
    }
}

internal sealed class SubmitApplicationHandler(
    ApplicationsDbContext db,
    IProgrammeReader programmes,
    IEligibilityChecker eligibility,
    IUserDirectory users,
    IFieldEncryptor encryptor,
    IOutbox outbox,
    ICurrentUser currentUser,
    ICurrentTenant tenant,
    IClock clock) : ICommandHandler<SubmitApplicationCommand, ApplicationView>
{
    public async Task<Result<ApplicationView>> Handle(SubmitApplicationCommand request, CancellationToken cancellationToken)
    {
        var learnerId = currentUser.UserId ?? Guid.Empty;
        var programme = await programmes.GetAsync(request.ProgrammeId, cancellationToken);
        if (programme is null || !programme.AcceptingApplications)
        {
            return Error.NotFound("opportunity_not_open", "This opportunity is not accepting applications.");
        }

        var application = await db.Applications.SingleOrDefaultAsync(a => a.ProgrammeId == programme.ProgrammeId && a.LearnerId == learnerId && a.Status != ApplicationStatus.Merged && a.Status != ApplicationStatus.Withdrawn, cancellationToken);
        if (application is null)
        {
            application = Application.Start(tenant.TenantId, programme.ProgrammeId, learnerId);
            db.Applications.Add(application);
        }

        var saved = application.SaveAnswers(request.Answers.GetRawText());
        if (saved.IsFailure)
        {
            return application.Status == ApplicationStatus.Submitted && request.Submit ? ApplicationView.From(application) : Result.Failure<ApplicationView>(saved.Error);
        }

        if (!request.Submit)
        {
            return ApplicationView.From(application);
        }

        var outcome = await eligibility.CheckAsync(programme.ProgrammeId, learnerId, AnswerFields.Facts(request.Answers), cancellationToken);
        if (!outcome.Eligible)
        {
            return Error.Validation("not_eligible", "You do not meet the eligibility rules: " + string.Join(", ", outcome.Reasons));
        }

        var nin = AnswerFields.Text(request.Answers, "nin");
        var ninHash = string.IsNullOrWhiteSpace(nin) ? null : encryptor.BlindIndex(nin.ToUpperInvariant());
        var user = await users.GetAsync(learnerId, cancellationToken);
        var phoneHash = string.IsNullOrEmpty(user?.PhoneHash) ? null : user.PhoneHash;
        var now = clock.UtcNow;
        var submitted = application.Submit(ninHash, phoneHash, now);
        if (submitted.IsFailure)
        {
            return Result.Failure<ApplicationView>(submitted.Error);
        }

        var duplicate = await db.Applications.Where(a => a.ProgrammeId == programme.ProgrammeId && a.Id != application.Id && a.LearnerId != learnerId && a.Status != ApplicationStatus.Merged
                && ((ninHash != null && a.NinHash == ninHash) || (phoneHash != null && a.PhoneHash == phoneHash)))
            .OrderBy(a => a.SubmittedAt).FirstOrDefaultAsync(cancellationToken);
        if (duplicate is not null)
        {
            application.MergeInto(duplicate.Id);
        }

        outbox.Enqueue(new ApplicationSubmitted(application.Id, application.Id, application.ProgrammeId, learnerId, application.IsFlagged));
        outbox.Enqueue(NotificationRequested.To(learnerId, "applications.application.submitted", new Dictionary<string, string> { ["applicationId"] = application.Id.ToString("D"), ["programme"] = programme.Name, ["hasAssessment"] = (programme.EntryAssessment is not null).ToString() }));
        return ApplicationView.From(application, true, outcome.Reasons);
    }
}

internal sealed class ListMyApplicationsHandler(ApplicationsDbContext db, ICurrentUser currentUser) : IQueryHandler<ListMyApplicationsQuery, IReadOnlyList<ApplicationView>>
{
    public async Task<Result<IReadOnlyList<ApplicationView>>> Handle(ListMyApplicationsQuery request, CancellationToken cancellationToken)
    {
        var mine = await db.Applications.AsNoTracking().Where(a => a.LearnerId == currentUser.UserId).OrderByDescending(a => a.CreatedAt).Take(100).ToListAsync(cancellationToken);
        return mine.Select(a => ApplicationView.From(a)).ToList();
    }
}

internal static class SubmitApplicationEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/programmes/{programmeId:guid}/applications", async (Guid programmeId, SubmitApplicationRequest body, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new SubmitApplicationCommand(programmeId, body.Answers, body.Submit ?? true), ct);
                return result.IsSuccess && result.Value.Status == "draft" ? Results.Ok(result.Value) : result.ToCreatedResult(r => $"/api/v1/applications/{r.ApplicationId}");
            })
            .WithName("SubmitApplication")
            .WithSummary("Learner: apply (≤ 8 fields). submit=false saves a draft to resume later; the same call again updates it.")
            .RequireAuthorization(Policies.Authenticated);

        group.MapGet("/me", async (ISender sender, CancellationToken ct) => (await sender.Send(new ListMyApplicationsQuery(), ct)).ToHttpResult())
            .WithName("ListMyApplications")
            .RequireAuthorization(Policies.Authenticated);
    }
}
