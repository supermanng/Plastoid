using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Wallet.Contracts;
using Plastoids.Modules.Wallet.Domain;
using Plastoids.Modules.Wallet.Persistence;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Wallet.Features.Admin.UnitRates;

/// <summary>Unit price configuration (ADR 0018): versioned, effective-from, four-eyes.</summary>
public sealed record ProposeUnitRateCommand(long RateKobo, DateTime EffectiveFrom, string Rationale, string IdempotencyKey) : ICommand<UnitRateVersionView>, IIdempotentCommand;

public sealed record ProposeUnitRateRequest(long RateKobo, DateTime EffectiveFrom, string Rationale);

public sealed record DecideUnitRateCommand(Guid RateId, string Decision, string? Note, string IdempotencyKey) : ICommand<UnitRateVersionView>, IIdempotentCommand;

public sealed record DecideUnitRateRequest(string Decision, string? Note);

public sealed record ListUnitRatesQuery : IQuery<UnitRatesView>;

public sealed record UnitRateVersionView(Guid RateId, long RateKobo, Money RateValue, int Version, DateTime EffectiveFrom, string Status, string Rationale, Guid ProposedBy, DateTime ProposedAt, Guid? DecidedBy, DateTime? DecidedAt, string? DecisionNote)
{
    public static UnitRateVersionView From(UnitRate r) => new(r.Id, r.RateKobo, new Money(r.RateKobo, Money.Naira), r.Version, r.EffectiveFrom, r.Status.ToString().ToLowerInvariant(), r.Rationale, r.ProposedBy, r.ProposedAt, r.DecidedBy, r.DecidedAt, r.DecisionNote);
}

public sealed record UnitRatesView(long EffectiveRateKobo, Money EffectiveRateValue, string EffectiveSource, IReadOnlyList<UnitRateVersionView> Rates);

internal sealed class ProposeUnitRateValidator : AbstractValidator<ProposeUnitRateCommand>
{
    public ProposeUnitRateValidator()
    {
        RuleFor(x => x.RateKobo).InclusiveBetween(UnitRate.MinRateKobo, UnitRate.MaxRateKobo);
        RuleFor(x => x.Rationale).NotEmpty().MaximumLength(1000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class DecideUnitRateValidator : AbstractValidator<DecideUnitRateCommand>
{
    public DecideUnitRateValidator()
    {
        RuleFor(x => x.RateId).NotEmpty();
        RuleFor(x => x.Decision).Must(d => d is "approved" or "rejected").WithMessage("decision must be approved or rejected.");
        RuleFor(x => x.Note).NotEmpty().MaximumLength(1000).When(x => x.Decision == "rejected");
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class ProposeUnitRateHandler(WalletDbContext db, ICurrentUser currentUser, ICurrentTenant tenant, IClock clock) : ICommandHandler<ProposeUnitRateCommand, UnitRateVersionView>
{
    public async Task<Result<UnitRateVersionView>> Handle(ProposeUnitRateCommand request, CancellationToken cancellationToken)
    {
        var version = (await db.Rates.MaxAsync(r => (int?)r.Version, cancellationToken) ?? 0) + 1;
        var rate = UnitRate.Propose(tenant.TenantId, request.RateKobo, request.EffectiveFrom, request.Rationale, version, currentUser.UserId ?? Guid.Empty, clock.UtcNow);
        if (rate.IsFailure)
        {
            return Result.Failure<UnitRateVersionView>(rate.Error);
        }

        db.Rates.Add(rate.Value);
        return UnitRateVersionView.From(rate.Value);
    }
}

internal sealed class DecideUnitRateHandler(WalletDbContext db, ICurrentUser currentUser, IClock clock) : ICommandHandler<DecideUnitRateCommand, UnitRateVersionView>
{
    public async Task<Result<UnitRateVersionView>> Handle(DecideUnitRateCommand request, CancellationToken cancellationToken)
    {
        var rate = await db.Rates.SingleOrDefaultAsync(r => r.Id == request.RateId, cancellationToken);
        if (rate is null)
        {
            return Error.NotFound("unit_rate_not_found", "No such unit rate.");
        }

        var staffId = currentUser.UserId ?? Guid.Empty;
        var decided = request.Decision == "approved" ? rate.Approve(staffId, clock.UtcNow) : rate.Reject(staffId, request.Note ?? string.Empty, clock.UtcNow);
        return decided.IsFailure ? Result.Failure<UnitRateVersionView>(decided.Error) : UnitRateVersionView.From(rate);
    }
}

internal sealed class ListUnitRatesHandler(WalletDbContext db, IUnitRates rates) : IQueryHandler<ListUnitRatesQuery, UnitRatesView>
{
    public async Task<Result<UnitRatesView>> Handle(ListUnitRatesQuery request, CancellationToken cancellationToken)
    {
        var effective = await rates.CurrentAsync(cancellationToken);
        var rows = await db.Rates.AsNoTracking().OrderByDescending(r => r.Version).Take(200).ToListAsync(cancellationToken);
        return new UnitRatesView(effective.RateKobo, new Money(effective.RateKobo, Money.Naira), effective.Source, rows.Select(UnitRateVersionView.From).ToList());
    }
}

internal static class UnitRateEndpoints
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapGet("/rates", async (ISender sender, CancellationToken ct) => (await sender.Send(new ListUnitRatesQuery(), ct)).ToHttpResult())
            .WithName("ListUnitRates")
            .WithSummary("Staff: the effective unit price and every proposed, approved and rejected version.")
            .RequireAuthorization(Policies.StaffAdmin);

        admin.MapPost("/rates", async (ProposeUnitRateRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new ProposeUnitRateCommand(body.RateKobo, body.EffectiveFrom, body.Rationale, key), ct)).ToCreatedResult(r => $"/api/v1/admin/wallet/rates/{r.RateId}");
            })
            .WithName("ProposeUnitRate")
            .WithSummary("Staff (maker): propose the naira value of one unit (kobo) with an effective-from date. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);

        admin.MapPost("/rates/{rateId:guid}/decision", async (Guid rateId, DecideUnitRateRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new DecideUnitRateCommand(rateId, body.Decision, body.Note, key), ct)).ToHttpResult();
            })
            .WithName("DecideUnitRate")
            .WithSummary("Staff (checker ≠ maker): approve or reject a proposed unit rate. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
