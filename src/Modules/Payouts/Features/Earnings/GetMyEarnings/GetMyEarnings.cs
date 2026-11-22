using System.Globalization;
using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payouts.Persistence;
using Plastoids.Modules.Payouts.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Payouts.Features.Earnings.GetMyEarnings;

/// <summary>Story G3: gross, commission, net, held, pending, paid and what the next run would pay.</summary>
public sealed record GetMyEarningsQuery : IQuery<EarningsStatementView>;

/// <summary>DownloadStatement: CSV of earnings and payouts in a date range.</summary>
public sealed record DownloadStatementQuery(DateOnly From, DateOnly To) : IQuery<StatementFile>;

public sealed record StatementFile(string FileName, string Csv);

internal sealed class GetMyEarningsValidator : AbstractValidator<GetMyEarningsQuery>
{
}

internal sealed class DownloadStatementValidator : AbstractValidator<DownloadStatementQuery>
{
    public DownloadStatementValidator()
    {
        RuleFor(x => x).Must(x => x.To >= x.From && x.To.DayNumber - x.From.DayNumber <= 366).WithMessage("The statement covers at most one year, from ≤ to.");
    }
}

internal sealed class GetMyEarningsHandler(EarningsStatements statements, ICurrentUser currentUser, IClock clock) : IQueryHandler<GetMyEarningsQuery, EarningsStatementView>
{
    public async Task<Result<EarningsStatementView>> Handle(GetMyEarningsQuery request, CancellationToken cancellationToken)
        => await statements.BuildAsync(PayeeTypes.Tutor, currentUser.UserId ?? Guid.Empty, clock.UtcNow, cancellationToken);
}

internal sealed class DownloadStatementHandler(PayoutsDbContext db, ICurrentUser currentUser) : IQueryHandler<DownloadStatementQuery, StatementFile>
{
    public async Task<Result<StatementFile>> Handle(DownloadStatementQuery request, CancellationToken cancellationToken)
    {
        var tutor = currentUser.UserId ?? Guid.Empty;
        var from = request.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = request.To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var earnings = await db.Earnings.AsNoTracking().Where(e => e.PayeeType == PayeeTypes.Tutor && e.PayeeId == tutor && e.EarnedAt >= from && e.EarnedAt < to).OrderBy(e => e.EarnedAt).ToListAsync(cancellationToken);
        var payouts = await db.Payouts.AsNoTracking().Where(p => p.PayeeType == PayeeTypes.Tutor && p.PayeeId == tutor && p.CreatedAt >= from && p.CreatedAt < to).OrderBy(p => p.CreatedAt).ToListAsync(cancellationToken);
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder("date,type,source,status,gross_kobo,commission_kobo,net_kobo,refunded_kobo,amount_kobo,reference\n");
        foreach (var e in earnings)
        {
            sb.Append(inv, $"{e.EarnedAt:yyyy-MM-dd},earning,{e.SourceType},{e.Status.ToString().ToLowerInvariant()},{e.Gross.AmountKobo},{e.Commission.AmountKobo},{e.Net.AmountKobo},{e.RefundedNet.AmountKobo},,{e.SourceKey}\n");
        }

        foreach (var p in payouts)
        {
            sb.Append(inv, $"{p.CreatedAt:yyyy-MM-dd},payout,bank,{p.Status.ToString().ToLowerInvariant()},,,,,{p.Amount.AmountKobo},{p.BankReference ?? p.ProviderReference}\n");
        }

        return new StatementFile($"statement-{request.From:yyyyMMdd}-{request.To:yyyyMMdd}.csv", sb.ToString());
    }
}

internal static class GetMyEarningsEndpoints
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/me", async (ISender sender, CancellationToken ct) => (await sender.Send(new GetMyEarningsQuery(), ct)).ToHttpResult())
            .WithName("GetMyEarnings")
            .WithSummary("Tutor: gross, commission, net, held, available, pending and paid, with recent payouts.")
            .RequireAuthorization(Policies.Tutor);

        group.MapGet("/me/statement", async (DateOnly? from, DateOnly? to, ISender sender, IClock clock, CancellationToken ct) =>
            {
                var today = DateOnly.FromDateTime(clock.UtcNow);
                var result = await sender.Send(new DownloadStatementQuery(from ?? today.AddMonths(-1), to ?? today), ct);
                return result.IsFailure ? ResultHttp.Problem(result.Error) : Results.Text(result.Value.Csv, "text/csv", Encoding.UTF8);
            })
            .WithName("DownloadStatement")
            .WithSummary("Tutor: CSV statement of earnings and payouts (?from=&to=, default last month).")
            .RequireAuthorization(Policies.Tutor);
    }
}
