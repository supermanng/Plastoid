using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Identity.Authorization;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Sponsorship.Features.Campaigns.CreateCampaign;

/// <summary>Stepper payload (story F1). Every field is optional so each step can be saved as a draft.</summary>
public sealed record CampaignStep(
    string? Name,
    string? Objectives,
    Guid? CourseId,
    Guid? OfferingId,
    int? Seats,
    Money? TuitionPerSeat,
    int? SponsorSharePercent,
    Money? Budget,
    JsonElement? EligibilityRules,
    JsonElement? EntryAssessment,
    JsonElement? ScoringRubric,
    DateTime? ApplicationsOpenAt,
    DateTime? ApplicationsCloseAt,
    DateTime? StartsAt,
    DateTime? EndsAt,
    string? SelectionMode,
    string? UnusedFundsPolicy);

public sealed record CreateCampaignCommand(Guid OrganisationId, CampaignStep Step) : ICommand<ProgrammeView>;

public sealed record SaveCampaignStepCommand(Guid OrganisationId, Guid ProgrammeId, CampaignStep Step) : ICommand<ProgrammeView>;

public sealed record GetProgrammeQuery(Guid OrganisationId, Guid ProgrammeId) : IQuery<ProgrammeView>;

public sealed record QuoteView(int Version, int Seats, Money TuitionPerSeatSponsorShare, Money Tuition, Money ManagementFee, Money SeatFee, Money Vat, Money Total, DateTime? QuotedAt, DateTime? ApprovedAt);

public sealed record ProgrammeView(
    Guid ProgrammeId,
    Guid OrganisationId,
    string Name,
    string Status,
    string ReviewStatus,
    string? Objectives,
    Guid? CourseId,
    Guid? OfferingId,
    string? CourseTitle,
    int Seats,
    Money TuitionPerSeat,
    int SponsorSharePercent,
    Money CoPayPerSeat,
    Money Budget,
    JsonElement EligibilityRules,
    bool HasEntryAssessment,
    bool HasScoringRubric,
    DateTime? ApplicationsOpenAt,
    DateTime? ApplicationsCloseAt,
    DateTime? StartsAt,
    DateTime? EndsAt,
    string SelectionMode,
    string UnusedFundsPolicy,
    QuoteView? Quote,
    Guid? InvoiceId,
    Money FundedAmount,
    bool IsFunded,
    bool PaymentPlanApproved,
    int SeatsUnlocked,
    DateTime? PublishedAt,
    DateTime? ClosedAt)
{
    public static ProgrammeView From(Programme p)
    {
        using var rules = JsonDocument.Parse(string.IsNullOrWhiteSpace(p.EligibilityRulesJson) ? "{}" : p.EligibilityRulesJson);
        var quote = p.QuotedAt is null ? null : new QuoteView(p.QuoteVersion, p.SeatCount, p.QuoteTuitionPerSeatShare, p.QuoteTuition, p.QuoteManagementFee, p.QuoteSeatFee, p.QuoteVat, p.QuoteTotal, p.QuotedAt, p.QuoteApprovedAt);
        return new ProgrammeView(p.Id, p.OrganisationId, p.Name, p.Status.ToString().ToLowerInvariant(), p.ReviewStatus.ToString().ToLowerInvariant(), p.Objectives, p.CourseId, p.OfferingId, p.CourseTitle,
            p.SeatCount, p.TuitionPerSeat, p.SponsorSharePercent, p.CoPayPerSeat, p.Budget, rules.RootElement.Clone(), p.EntryAssessmentJson is not null, p.ScoringRubricJson is not null,
            p.ApplicationsOpenAt, p.ApplicationsCloseAt, p.StartsAt, p.EndsAt, p.SelectionMode, p.UnusedFundsPolicy, quote, p.InvoiceId, p.FundedAmount, p.IsFunded, p.PaymentPlanApproved, p.SeatsUnlocked, p.PublishedAt, p.ClosedAt);
    }
}

internal sealed class CampaignStepValidator : AbstractValidator<CampaignStep>
{
    public CampaignStepValidator()
    {
        RuleFor(x => x.Name).MaximumLength(200);
        RuleFor(x => x.Objectives).MaximumLength(2000);
        RuleFor(x => x.Seats).InclusiveBetween(1, 100_000).When(x => x.Seats is not null);
        RuleFor(x => x.SponsorSharePercent).InclusiveBetween(1, 100).When(x => x.SponsorSharePercent is not null);
        RuleFor(x => x.TuitionPerSeat!.Value.AmountKobo).GreaterThan(0).When(x => x.TuitionPerSeat is not null);
        RuleFor(x => x.Budget!.Value.AmountKobo).GreaterThanOrEqualTo(0).When(x => x.Budget is not null);
        RuleFor(x => x.SelectionMode!).Must(SelectionModes.IsValid).When(x => x.SelectionMode is not null).WithMessage("selectionMode must be plastoids, sponsor or delegated.");
        RuleFor(x => x.UnusedFundsPolicy!).Must(UnusedFundsPolicies.IsValid).When(x => x.UnusedFundsPolicy is not null).WithMessage("unusedFundsPolicy must be refund or rollover.");
        RuleFor(x => x.EligibilityRules).Must(r => r!.Value.ValueKind == JsonValueKind.Object && EligibilityEngine.ValidateRules(r.Value.GetRawText()).IsSuccess)
            .When(x => x.EligibilityRules is not null)
            .WithMessage(x => EligibilityEngine.ValidateRules(x.EligibilityRules!.Value.ValueKind == JsonValueKind.Object ? x.EligibilityRules.Value.GetRawText() : "[]").Error.Message);
        RuleFor(x => x.EntryAssessment).Must(a => a!.Value.ValueKind == JsonValueKind.Object && a.Value.TryGetProperty("questions", out var q) && q.ValueKind == JsonValueKind.Array && q.GetArrayLength() > 0)
            .When(x => x.EntryAssessment is not null).WithMessage("entryAssessment needs a questions array.");
        RuleFor(x => x.ScoringRubric).Must(r => r!.Value.ValueKind == JsonValueKind.Object && r.Value.TryGetProperty("criteria", out var c) && c.ValueKind == JsonValueKind.Array
                && c.EnumerateArray().All(k => k.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String && !EligibilityEngine.ProtectedCharacteristics.Contains(key.GetString()!)))
            .When(x => x.ScoringRubric is not null).WithMessage("scoringRubric needs criteria [{ key, weight, max }] and no protected characteristic.");
        RuleFor(x => x).Must(x => x.EndsAt > x.StartsAt).When(x => x.StartsAt is not null && x.EndsAt is not null).WithMessage("endsAt must be after startsAt.");
    }
}

internal sealed class CreateCampaignValidator : AbstractValidator<CreateCampaignCommand>
{
    public CreateCampaignValidator()
    {
        RuleFor(x => x.OrganisationId).NotEmpty();
        RuleFor(x => x.Step.Name).NotEmpty().WithMessage("A campaign name is required to start a draft.");
        RuleFor(x => x.Step).SetValidator(new CampaignStepValidator());
    }
}

internal sealed class SaveCampaignStepValidator : AbstractValidator<SaveCampaignStepCommand>
{
    public SaveCampaignStepValidator()
    {
        RuleFor(x => x.OrganisationId).NotEmpty();
        RuleFor(x => x.ProgrammeId).NotEmpty();
        RuleFor(x => x.Step).SetValidator(new CampaignStepValidator());
    }
}

internal static class StepMapper
{
    /// <summary>Resolves the chosen course/offering through Catalogue.Contracts: the offering must accept sponsorship; tuition defaults to its price.</summary>
    public static async Task<Result<ProgrammeDraft>> ToDraftAsync(CampaignStep step, ICourseOutlineReader courses, CancellationToken ct)
    {
        string? courseTitle = null;
        Guid? tutorUserId = null;
        var tuition = step.TuitionPerSeat;
        if (step.CourseId is { } courseId)
        {
            var course = await courses.GetPublishedAsync(courseId, null, ct);
            if (course is null)
            {
                return Error.NotFound("course_not_found", "No published course with that id.");
            }

            var offering = course.Offerings.SingleOrDefault(o => o.OfferingId == step.OfferingId);
            if (step.OfferingId is not null && offering is null)
            {
                return Error.NotFound("offering_not_found", "The course has no such offering.");
            }

            if (offering is { AcceptsSponsorship: false })
            {
                return Error.Validation("offering_not_sponsorable", "The offering does not accept sponsored funding.");
            }

            courseTitle = course.Title;
            tutorUserId = course.OwnerUserId;
            tuition ??= offering?.Price;
        }

        return new ProgrammeDraft(step.Objectives, step.CourseId, step.OfferingId, courseTitle, tutorUserId, step.Seats, tuition, step.SponsorSharePercent, step.Budget,
            step.EligibilityRules?.GetRawText(), step.EntryAssessment?.GetRawText(), step.ScoringRubric?.GetRawText(), step.ApplicationsOpenAt, step.ApplicationsCloseAt, step.StartsAt, step.EndsAt, step.SelectionMode, step.UnusedFundsPolicy);
    }
}

internal sealed class CreateCampaignHandler(SponsorshipDbContext db, IOrganisationDirectory organisations, ICourseOutlineReader courses, ICurrentUser currentUser, ICurrentTenant tenant) : ICommandHandler<CreateCampaignCommand, ProgrammeView>
{
    public async Task<Result<ProgrammeView>> Handle(CreateCampaignCommand request, CancellationToken cancellationToken)
    {
        var organisation = await organisations.GetAsync(request.OrganisationId, cancellationToken);
        if (organisation is null)
        {
            return Error.NotFound("organisation_not_found", "No such organisation.");
        }

        if (!organisation.IsVerified)
        {
            return Error.Conflict("organisation_not_verified", "Campaigns can be created once the organisation's CAC and signatory are verified (blueprint §10, week −8).");
        }

        var draft = await StepMapper.ToDraftAsync(request.Step, courses, cancellationToken);
        if (draft.IsFailure)
        {
            return Result.Failure<ProgrammeView>(draft.Error);
        }

        var programme = Programme.Create(tenant.TenantId, organisation.OrganisationId, organisation.Name, request.Step.Name!, currentUser.UserId ?? Guid.Empty);
        var saved = programme.SaveDraft(null, draft.Value);
        if (saved.IsFailure)
        {
            return Result.Failure<ProgrammeView>(saved.Error);
        }

        db.Programmes.Add(programme);
        return ProgrammeView.From(programme);
    }
}

internal sealed class SaveCampaignStepHandler(ProgrammeAccess access, ICourseOutlineReader courses) : ICommandHandler<SaveCampaignStepCommand, ProgrammeView>
{
    public async Task<Result<ProgrammeView>> Handle(SaveCampaignStepCommand request, CancellationToken cancellationToken)
    {
        var programme = await access.ForOrganisationAsync(request.OrganisationId, request.ProgrammeId, cancellationToken);
        if (programme.IsFailure)
        {
            return Result.Failure<ProgrammeView>(programme.Error);
        }

        var draft = await StepMapper.ToDraftAsync(request.Step, courses, cancellationToken);
        if (draft.IsFailure)
        {
            return Result.Failure<ProgrammeView>(draft.Error);
        }

        var saved = programme.Value.SaveDraft(request.Step.Name, draft.Value);
        return saved.IsFailure ? Result.Failure<ProgrammeView>(saved.Error) : ProgrammeView.From(programme.Value);
    }
}

internal sealed class GetProgrammeHandler(SponsorshipDbContext db, ICurrentUser currentUser) : IQueryHandler<GetProgrammeQuery, ProgrammeView>
{
    public async Task<Result<ProgrammeView>> Handle(GetProgrammeQuery request, CancellationToken cancellationToken)
    {
        var programme = await db.Programmes.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.ProgrammeId, cancellationToken);
        if (programme is null || (programme.OrganisationId != request.OrganisationId && !currentUser.Roles.Contains("staff")))
        {
            return Error.NotFound("programme_not_found", "No such programme for this organisation.");
        }

        return ProgrammeView.From(programme);
    }
}

internal static class CreateCampaignEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/", async (Guid organisationId, CampaignStep body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CreateCampaignCommand(organisationId, body), ct)).ToCreatedResult(r => $"/api/v1/sponsorship/organisations/{r.OrganisationId}/programmes/{r.ProgrammeId}"))
            .WithName("CreateCampaign")
            .WithSummary("Sponsor: start a campaign draft (stepper step 1). Only verified organisations.")
            .RequireAuthorization(IdentityPolicies.OrganisationMember);

        group.MapPut("/{programmeId:guid}", async (Guid organisationId, Guid programmeId, CampaignStep body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SaveCampaignStepCommand(organisationId, programmeId, body), ct)).ToHttpResult())
            .WithName("SaveCampaignStep")
            .WithSummary("Sponsor: save a stepper step (objectives, course/offering, seats, budget, eligibility rules, dates, selection mode) on the draft.")
            .RequireAuthorization(IdentityPolicies.OrganisationMember);

        group.MapGet("/{programmeId:guid}", async (Guid organisationId, Guid programmeId, ISender sender, CancellationToken ct) =>
                (await sender.Send(new GetProgrammeQuery(organisationId, programmeId), ct)).ToHttpResult())
            .WithName("GetProgramme")
            .RequireAuthorization(IdentityPolicies.OrganisationMember);
    }
}
