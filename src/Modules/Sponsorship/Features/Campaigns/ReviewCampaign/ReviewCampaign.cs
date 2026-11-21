using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Messaging;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Sponsorship.Features.Campaigns.ReviewCampaign;

/// <summary>Plastoids programme review: approve / changes_requested / rejected, with the lawful-rules attestation and an optional payment plan (tranches).</summary>
public sealed record ReviewCampaignCommand(Guid ProgrammeId, string Decision, bool RulesLawful, string? Notes, JsonElement? PaymentPlan, string IdempotencyKey) : ICommand<ReviewCampaignResponse>, IIdempotentCommand;

public sealed record ReviewCampaignRequest(string Decision, bool RulesLawful, string? Notes, JsonElement? PaymentPlan);

public sealed record ReviewCampaignResponse(Guid ProgrammeId, string ReviewStatus, bool PaymentPlanApproved);

internal sealed class ReviewCampaignValidator : AbstractValidator<ReviewCampaignCommand>
{
    public ReviewCampaignValidator()
    {
        RuleFor(x => x.ProgrammeId).NotEmpty();
        RuleFor(x => x.Decision).Must(d => d is "approved" or "changes_requested" or "rejected").WithMessage("decision must be approved, changes_requested or rejected.");
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.Notes).NotEmpty().When(x => x.Decision != "approved").WithMessage("Say what must change.");
        RuleFor(x => x.PaymentPlan).Must(p => p!.Value.ValueKind == JsonValueKind.Object && p.Value.TryGetProperty("tranches", out var t) && t.ValueKind == JsonValueKind.Array && t.GetArrayLength() is > 0 and <= 12)
            .When(x => x.PaymentPlan is not null).WithMessage("paymentPlan needs 1–12 tranches [{ dueAt, amount }].");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class ReviewCampaignHandler(ProgrammeAccess access, IOutbox outbox, ICurrentUser currentUser, IClock clock) : ICommandHandler<ReviewCampaignCommand, ReviewCampaignResponse>
{
    public async Task<Result<ReviewCampaignResponse>> Handle(ReviewCampaignCommand request, CancellationToken cancellationToken)
    {
        var loaded = await access.ForStaffAsync(request.ProgrammeId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result.Failure<ReviewCampaignResponse>(loaded.Error);
        }

        var programme = loaded.Value;
        var rules = EligibilityEngine.ValidateRules(programme.EligibilityRulesJson);
        if (rules.IsFailure)
        {
            return Result.Failure<ReviewCampaignResponse>(rules.Error);
        }

        var decision = request.Decision switch
        {
            "approved" => ReviewStatus.Approved,
            "changes_requested" => ReviewStatus.ChangesRequested,
            _ => ReviewStatus.Rejected,
        };
        var reviewed = programme.Review(currentUser.UserId ?? Guid.Empty, decision, request.RulesLawful, request.Notes, request.PaymentPlan?.GetRawText(), clock.UtcNow);
        if (reviewed.IsFailure)
        {
            return Result.Failure<ReviewCampaignResponse>(reviewed.Error);
        }

        outbox.Enqueue(NotificationRequested.To(programme.CreatedBy, "sponsorship.programme.reviewed", new Dictionary<string, string> { ["programmeId"] = programme.Id.ToString("D"), ["decision"] = request.Decision, ["notes"] = request.Notes ?? string.Empty }));
        return new ReviewCampaignResponse(programme.Id, programme.ReviewStatus.ToString().ToLowerInvariant(), programme.PaymentPlanApproved);
    }
}

internal static class ReviewCampaignEndpoint
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapPost("/{programmeId:guid}/review", async (Guid programmeId, ReviewCampaignRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new ReviewCampaignCommand(programmeId, body.Decision, body.RulesLawful, body.Notes, body.PaymentPlan, key), ct)).ToHttpResult();
            })
            .WithName("ReviewCampaign")
            .WithSummary("Staff: programme review with the lawful/non-discriminatory rules attestation; an approved payment plan unlocks seats before full payment. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
