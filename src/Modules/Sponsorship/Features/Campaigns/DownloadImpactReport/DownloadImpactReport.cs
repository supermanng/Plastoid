using System.Globalization;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Modules.Identity.Authorization;
using Plastoids.Modules.Sponsorship.Persistence;
using Plastoids.Modules.Sponsorship.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Sponsorship.Features.Campaigns.DownloadImpactReport;

/// <summary>MVP impact report (blueprint §10 week 14): funnel, attendance, completion and scores as a PDF. Narrative and demographics are Phase 2.</summary>
public sealed record DownloadImpactReportQuery(Guid OrganisationId, Guid ProgrammeId) : IQuery<ImpactReportFile>;

public sealed record ImpactReportFile(string FileName, byte[] Content);

internal sealed class DownloadImpactReportValidator : AbstractValidator<DownloadImpactReportQuery>
{
    public DownloadImpactReportValidator()
    {
        RuleFor(x => x.ProgrammeId).NotEmpty();
    }
}

internal sealed class DownloadImpactReportHandler(SponsorshipDbContext db, ICurrentUser currentUser, IClock clock) : IQueryHandler<DownloadImpactReportQuery, ImpactReportFile>
{
    public async Task<Result<ImpactReportFile>> Handle(DownloadImpactReportQuery request, CancellationToken cancellationToken)
    {
        var programme = await db.Programmes.AsNoTracking().SingleOrDefaultAsync(p => p.Id == request.ProgrammeId, cancellationToken);
        if (programme is null || (programme.OrganisationId != request.OrganisationId && !currentUser.Roles.Contains("staff")))
        {
            return Error.NotFound("programme_not_found", "No such programme for this organisation.");
        }

        var funnel = await FunnelReader.BuildAsync(db, programme, cancellationToken);
        var sessions = await db.Attendance.AsNoTracking().Where(a => a.ProgrammeId == programme.Id).Select(a => a.LiveSessionId).Distinct().CountAsync(cancellationToken);
        var completed = funnel.Stages.Single(s => s.Stage == "completed").Count;
        var completionRate = funnel.SeatsFilled == 0 ? 0 : Math.Round(100m * completed / funnel.SeatsFilled, 1);
        var inv = CultureInfo.InvariantCulture;
        var lines = new List<string>
        {
            "Plastoids - Programme impact report",
            $"Programme: {programme.Name}",
            $"Sponsor: {programme.SponsorName}",
            $"Course: {programme.CourseTitle}",
            $"Period: {programme.StartsAt?.ToString("yyyy-MM-dd", inv)} to {programme.EndsAt?.ToString("yyyy-MM-dd", inv)}",
            $"Generated: {clock.UtcNow.ToString("yyyy-MM-dd HH:mm", inv)} UTC",
            string.Empty,
            "Reach",
            $"  Seats funded: {programme.SeatsUnlocked}   Seats filled: {funnel.SeatsFilled}   Unfilled: {funnel.SeatsOpen}",
        };
        foreach (var stage in funnel.Stages)
        {
            lines.Add($"  {stage.Stage,-10} {stage.Count,6}   ({stage.PercentOfApplied.ToString(inv)}% of applied)");
        }

        lines.AddRange(
        [
            string.Empty,
            "Attendance",
            $"  Live sessions held: {sessions}   Attendances recorded: {funnel.SessionsAttended}   Per filled seat: {funnel.AttendancePerSeat.ToString(inv)}",
            string.Empty,
            "Outcomes",
            $"  Completion rate: {completionRate.ToString(inv)}%   Certified: {funnel.Stages.Single(s => s.Stage == "certified").Count}",
            $"  Average assessment score: {(funnel.AverageScore?.ToString(inv) ?? "n/a")}",
            string.Empty,
            "Funds",
            $"  Invoice: {programme.QuoteTotal}   Received: {programme.FundedAmount}   Close-out refund: {programme.CloseoutRefund}",
            string.Empty,
            "Demographics are aggregated and reported once the programme has at least 20 beneficiaries (Phase 2).",
        ]);
        var pdf = SimplePdf.FromLines(lines);
        return new ImpactReportFile($"impact-report-{programme.Id:N}.pdf", pdf);
    }
}

internal static class DownloadImpactReportEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapGet("/{programmeId:guid}/impact-report", async (Guid organisationId, Guid programmeId, ISender sender, CancellationToken ct) =>
            {
                var result = await sender.Send(new DownloadImpactReportQuery(organisationId, programmeId), ct);
                return result.IsFailure ? ResultHttp.Problem(result.Error) : Results.File(result.Value.Content, "application/pdf", result.Value.FileName);
            })
            .WithName("DownloadImpactReport")
            .WithSummary("Sponsor: PDF with funnel, attendance, completion and scores.")
            .RequireAuthorization(IdentityPolicies.OrganisationMember);
    }
}
