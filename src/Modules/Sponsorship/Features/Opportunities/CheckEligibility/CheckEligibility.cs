using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Sponsorship.Contracts;
using Plastoids.Modules.Sponsorship.Domain;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Sponsorship.Features.Opportunities.CheckEligibility;

/// <summary>Story F2: deterministic rule check in well under a second; ineligible answers carry reasons and alternative opportunities.</summary>
public sealed record CheckEligibilityQuery(Guid ProgrammeId, ApplicantFacts Facts) : IQuery<EligibilityView>;

public sealed record AlternativeView(Guid ProgrammeId, string Name, string SponsorName, DateTime? ApplicationsCloseAt);

public sealed record EligibilityView(Guid ProgrammeId, bool Eligible, IReadOnlyList<string> Reasons, IReadOnlyList<AlternativeView> Alternatives, int EvaluationMs);

internal sealed class CheckEligibilityValidator : AbstractValidator<CheckEligibilityQuery>
{
    public CheckEligibilityValidator()
    {
        RuleFor(x => x.ProgrammeId).NotEmpty();
        RuleFor(x => x.Facts.CountryOfResidence).MaximumLength(2);
        RuleFor(x => x.Facts.StateOfResidence).MaximumLength(40);
        RuleFor(x => x.Facts.EducationLevel).MaximumLength(40);
        RuleFor(x => x.Facts.EmploymentStatus).MaximumLength(40);
    }
}

internal sealed class CheckEligibilityHandler(SponsorshipDbContext db, IUserDirectory users, ICurrentUser currentUser, IClock clock) : IQueryHandler<CheckEligibilityQuery, EligibilityView>
{
    public async Task<Result<EligibilityView>> Handle(CheckEligibilityQuery request, CancellationToken cancellationToken)
    {
        var started = System.Diagnostics.Stopwatch.StartNew();
        var now = clock.UtcNow;
        var programme = await db.Programmes.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.ProgrammeId, cancellationToken);
        if (programme is null || !programme.IsAcceptingApplications(now))
        {
            return Error.NotFound("opportunity_not_open", "This opportunity is not accepting applications.");
        }

        var learnerId = currentUser.UserId ?? Guid.Empty;
        var user = await users.GetAsync(learnerId, cancellationToken);
        var previouslySponsored = await db.Seats.AsNoTracking().AnyAsync(s => s.LearnerId == learnerId && s.Status == SeatStatus.Filled, cancellationToken);
        using var rules = JsonDocument.Parse(string.IsNullOrWhiteSpace(programme.EligibilityRulesJson) ? "{}" : programme.EligibilityRulesJson);
        var outcome = EligibilityEngine.Evaluate(rules.RootElement, user?.AgeYears, request.Facts, previouslySponsored);

        var alternatives = new List<AlternativeView>();
        if (!outcome.Eligible)
        {
            var others = await db.Programmes.AsNoTracking().Where(p => p.Id != programme.Id && p.Status == ProgrammeStatus.Published && p.ApplicationsCloseAt > now).OrderBy(p => p.ApplicationsCloseAt).Take(25).ToListAsync(cancellationToken);
            foreach (var other in others)
            {
                using var otherRules = JsonDocument.Parse(string.IsNullOrWhiteSpace(other.EligibilityRulesJson) ? "{}" : other.EligibilityRulesJson);
                if (EligibilityEngine.Evaluate(otherRules.RootElement, user?.AgeYears, request.Facts, previouslySponsored && other.Id != programme.Id).Eligible)
                {
                    alternatives.Add(new AlternativeView(other.Id, other.Name, other.SponsorName, other.ApplicationsCloseAt));
                }

                if (alternatives.Count == 5)
                {
                    break;
                }
            }
        }

        return new EligibilityView(programme.Id, outcome.Eligible, outcome.Reasons, alternatives, (int)started.ElapsedMilliseconds);
    }
}

internal static class CheckEligibilityEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{programmeId:guid}/eligibility", async (Guid programmeId, ApplicantFacts body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new CheckEligibilityQuery(programmeId, body), ct)).ToHttpResult())
            .WithName("CheckEligibility")
            .WithSummary("Learner: instant eligibility check with reasons and alternatives. Age comes from the verified profile; other facts are self-declared.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
