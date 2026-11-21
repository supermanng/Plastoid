using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Payments.Domain;
using Plastoids.Modules.Payments.Persistence;
using Plastoids.Modules.Payments.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payments.Features.Admin.FeeRules;

/// <summary>Fee-rule configuration (blueprint §9.20): versioned, effective-from, four-eyes.</summary>
public sealed record ProposeFeeRuleCommand(string RuleCode, int CommissionBps, DateTime EffectiveFrom, string Rationale, string IdempotencyKey) : ICommand<FeeRuleView>, IIdempotentCommand;

public sealed record ProposeFeeRuleRequest(string RuleCode, int CommissionBps, DateTime EffectiveFrom, string Rationale);

public sealed record DecideFeeRuleCommand(Guid RuleId, string Decision, string? Note, string IdempotencyKey) : ICommand<FeeRuleView>, IIdempotentCommand;

public sealed record DecideFeeRuleRequest(string Decision, string? Note);

public sealed record ListFeeRulesQuery(string? RuleCode) : IQuery<FeeRulesView>;

public sealed record FeeRuleView(Guid RuleId, string RuleCode, int CommissionBps, int Version, DateTime EffectiveFrom, string Status, string Rationale, Guid ProposedBy, DateTime ProposedAt, Guid? DecidedBy, DateTime? DecidedAt, string? DecisionNote)
{
    public static FeeRuleView From(FeeRule r) => new(r.Id, r.RuleCode, r.CommissionBps, r.Version, r.EffectiveFrom, r.Status.ToString().ToLowerInvariant(), r.Rationale, r.ProposedBy, r.ProposedAt, r.DecidedBy, r.DecidedAt, r.DecisionNote);
}

public sealed record EffectiveRateView(string RuleCode, int CommissionBps, string Source);

public sealed record FeeRulesView(IReadOnlyList<EffectiveRateView> EffectiveNow, IReadOnlyList<FeeRuleView> Rules);

internal sealed class ProposeFeeRuleValidator : AbstractValidator<ProposeFeeRuleCommand>
{
    public ProposeFeeRuleValidator()
    {
        RuleFor(x => x.RuleCode).NotEmpty().Must(FeeRule.Codes.Contains).WithMessage("Unknown ruleCode.");
        RuleFor(x => x.CommissionBps).InclusiveBetween(0, 5000);
        RuleFor(x => x.Rationale).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class DecideFeeRuleValidator : AbstractValidator<DecideFeeRuleCommand>
{
    public DecideFeeRuleValidator()
    {
        RuleFor(x => x.RuleId).NotEmpty();
        RuleFor(x => x.Decision).Must(d => d is "approved" or "rejected").WithMessage("decision must be approved or rejected.");
        RuleFor(x => x.Note).NotEmpty().MaximumLength(1000).When(x => x.Decision == "rejected");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class ProposeFeeRuleHandler(PaymentsDbContext db, ICurrentUser currentUser, ICurrentTenant tenant, IClock clock) : ICommandHandler<ProposeFeeRuleCommand, FeeRuleView>
{
    public async Task<Result<FeeRuleView>> Handle(ProposeFeeRuleCommand request, CancellationToken cancellationToken)
    {
        var version = await db.FeeRules.CountAsync(r => r.RuleCode == request.RuleCode, cancellationToken) + 1;
        var rule = FeeRule.Propose(tenant.TenantId, request.RuleCode, request.CommissionBps, request.EffectiveFrom, request.Rationale, version, currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        if (rule.IsFailure)
        {
            return Result.Failure<FeeRuleView>(rule.Error);
        }

        db.FeeRules.Add(rule.Value);
        return FeeRuleView.From(rule.Value);
    }
}

internal sealed class DecideFeeRuleHandler(PaymentsDbContext db, ICurrentUser currentUser, IClock clock) : ICommandHandler<DecideFeeRuleCommand, FeeRuleView>
{
    public async Task<Result<FeeRuleView>> Handle(DecideFeeRuleCommand request, CancellationToken cancellationToken)
    {
        var rule = await db.FeeRules.SingleOrDefaultAsync(r => r.Id == request.RuleId, cancellationToken);
        if (rule is null)
        {
            return Error.NotFound("fee_rule_not_found", "No such fee rule.");
        }

        var staffId = currentUser.UserId ?? Guid.Empty;
        var decided = request.Decision == "approved" ? rule.Approve(staffId, clock.UtcNow) : rule.Reject(staffId, request.Note ?? string.Empty, clock.UtcNow);
        return decided.IsFailure ? Result.Failure<FeeRuleView>(decided.Error) : FeeRuleView.From(rule);
    }
}

internal sealed class ListFeeRulesHandler(PaymentsDbContext db, IOptions<FeeOptions> options, IClock clock) : IQueryHandler<ListFeeRulesQuery, FeeRulesView>
{
    public async Task<Result<FeeRulesView>> Handle(ListFeeRulesQuery request, CancellationToken cancellationToken)
    {
        var query = db.FeeRules.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.RuleCode))
        {
            query = query.Where(r => r.RuleCode == request.RuleCode);
        }

        var rules = await query.OrderByDescending(r => r.ProposedAt).Take(200).ToListAsync(cancellationToken);
        var approved = await db.FeeRules.AsNoTracking().Where(r => r.Status == FeeRuleStatus.Approved).ToListAsync(cancellationToken);
        var now = clock.UtcNow;
        var effective = FeeRule.Codes.Select(code =>
        {
            var rule = FeeRule.EffectiveAt(approved, code, now);
            return rule is null ? new EffectiveRateView(code, Services.FeeRules.DefaultBps(code, options.Value), "default") : new EffectiveRateView(code, rule.CommissionBps, $"v{rule.Version}");
        }).ToList();
        return new FeeRulesView(effective, rules.Select(FeeRuleView.From).ToList());
    }
}

internal static class FeeRuleEndpoints
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapGet("/fee-rules", async (string? ruleCode, ISender sender, CancellationToken ct) => (await sender.Send(new ListFeeRulesQuery(ruleCode), ct)).ToHttpResult())
            .WithName("ListFeeRules")
            .WithSummary("Staff: effective commission per rule code and the versioned rule history.")
            .RequireAuthorization(Policies.StaffAdmin);

        admin.MapPost("/fee-rules", async (ProposeFeeRuleRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new ProposeFeeRuleCommand(body.RuleCode, body.CommissionBps, body.EffectiveFrom, body.Rationale, key), ct)).ToCreatedResult(r => $"/api/v1/admin/payments/fee-rules/{r.RuleId}");
            })
            .WithName("ProposeFeeRule")
            .WithSummary("Staff (maker): propose a fee rule version with an effective-from date. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);

        admin.MapPost("/fee-rules/{ruleId:guid}/decision", async (Guid ruleId, DecideFeeRuleRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new DecideFeeRuleCommand(ruleId, body.Decision, body.Note, key), ct)).ToHttpResult();
            })
            .WithName("DecideFeeRule")
            .WithSummary("Staff (checker ≠ maker): approve or reject the proposed rule. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
