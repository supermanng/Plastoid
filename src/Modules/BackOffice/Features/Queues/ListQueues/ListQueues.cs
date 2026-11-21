using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.BackOffice.Domain;
using Plastoids.Modules.BackOffice.Persistence;
using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Payments.Contracts;
using Plastoids.Modules.Payouts.Contracts;
using Plastoids.Modules.Tutors.Contracts;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.BackOffice.Features.Queues.ListQueues;

/// <summary>Work queues (blueprint §9.20): verification, course moderation, refunds, disputes, data requests. Views only; decisions stay on each module's /admin endpoints.</summary>
public sealed record GetVerificationQueuesQuery(int? Limit) : IQuery<VerificationQueuesView>;

public sealed record VerificationQueuesView(IReadOnlyList<TutorVerificationItem> Tutors, IReadOnlyList<OrganisationAdminSummary> Organisations, string DecideTutorsAt, string DecideOrganisationsAt);

public sealed record GetCourseModerationQueueQuery(string? Status, int? Limit) : IQuery<CourseModerationQueueView>;

public sealed record CourseModerationQueueView(IReadOnlyList<CourseModerationItem> Courses, string DecideAt);

public sealed record GetRefundQueueQuery(int? Limit) : IQuery<RefundQueueView>;

public sealed record RefundQueueView(IReadOnlyList<PendingRefundItem> Refunds, string DecideAt);

public sealed record GetDisputeQueueQuery(int? Limit) : IQuery<DisputeQueueView>;

public sealed record StrikeAlertView(Guid AlertId, Guid? TutorId, string Message, DateTime RaisedAt);

public sealed record DisputeQueueView(IReadOnlyList<DisputeHoldItem> Holds, IReadOnlyList<StrikeAlertView> FlaggedTutors, string ReleaseHoldsAt);

public sealed record GetDataRequestQueueQuery(string? Status, int? Limit) : IQuery<IReadOnlyList<DataRequestSummary>>;

internal sealed class GetVerificationQueuesValidator : AbstractValidator<GetVerificationQueuesQuery>
{
    public GetVerificationQueuesValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 200).When(x => x.Limit is not null);
    }
}

internal sealed class GetVerificationQueuesHandler(ITutorVerificationQueue tutors, IUserAdministration users, IClock clock) : IQueryHandler<GetVerificationQueuesQuery, VerificationQueuesView>
{
    public async Task<Result<VerificationQueuesView>> Handle(GetVerificationQueuesQuery request, CancellationToken ct)
        => new VerificationQueuesView(await tutors.ListPendingAsync(clock.UtcNow, request.Limit ?? 50, ct), await users.ListOrganisationsAsync("pending", request.Limit ?? 50, ct),
            "/api/v1/admin/tutors/*", "/api/v1/admin/identity/organisations/{organisationId}/verification");
}

internal sealed class GetCourseModerationQueueHandler(ICourseModeration moderation) : IQueryHandler<GetCourseModerationQueueQuery, CourseModerationQueueView>
{
    public async Task<Result<CourseModerationQueueView>> Handle(GetCourseModerationQueueQuery request, CancellationToken ct)
        => new CourseModerationQueueView(await moderation.ListAsync(request.Status, request.Limit ?? 50, ct), "/api/v1/admin/catalogue/courses/{courseId}/review");
}

internal sealed class GetRefundQueueHandler(IPaymentsAdminQueues payments) : IQueryHandler<GetRefundQueueQuery, RefundQueueView>
{
    public async Task<Result<RefundQueueView>> Handle(GetRefundQueueQuery request, CancellationToken ct)
        => new RefundQueueView(await payments.ListPendingRefundsAsync(request.Limit ?? 50, ct), "/api/v1/admin/payments/refunds/{refundId}/approve");
}

internal sealed class GetDisputeQueueHandler(IDisputeQueue disputes, BackOfficeDbContext db, IClock clock) : IQueryHandler<GetDisputeQueueQuery, DisputeQueueView>
{
    public async Task<Result<DisputeQueueView>> Handle(GetDisputeQueueQuery request, CancellationToken ct)
    {
        var holds = await disputes.ListOpenAsync(clock.UtcNow, request.Limit ?? 50, ct);
        var strikes = await db.Alerts.AsNoTracking().Where(a => a.Kind == AlertKinds.TutorStrikes && a.ResolvedAt == null).OrderByDescending(a => a.RaisedAt).Take(request.Limit ?? 50)
            .Select(a => new StrikeAlertView(a.Id, a.ReferenceId, a.Message, a.RaisedAt)).ToListAsync(ct);
        return new DisputeQueueView(holds, strikes, "/api/v1/admin/payouts/holds/{holdId}/release");
    }
}

internal sealed class GetDataRequestQueueHandler(IUserAdministration users) : IQueryHandler<GetDataRequestQueueQuery, IReadOnlyList<DataRequestSummary>>
{
    public async Task<Result<IReadOnlyList<DataRequestSummary>>> Handle(GetDataRequestQueueQuery request, CancellationToken ct)
        => Result.Success(await users.ListDataRequestsAsync(request.Status ?? "pending", request.Limit ?? 50, ct));
}

internal static class QueueEndpoints
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapGet("/queues/verification", async (int? limit, ISender sender, CancellationToken ct) => (await sender.Send(new GetVerificationQueuesQuery(limit), ct)).ToHttpResult())
            .WithName("BackOfficeVerificationQueues").WithSummary("Staff: pending tutor checks (with SLA) and organisations awaiting verification.").RequireAuthorization(Policies.StaffAdmin);
        admin.MapGet("/queues/course-moderation", async (string? status, int? limit, ISender sender, CancellationToken ct) => (await sender.Send(new GetCourseModerationQueueQuery(status, limit), ct)).ToHttpResult())
            .WithName("BackOfficeCourseModerationQueue").WithSummary("Staff: courses in review (decide on /admin/catalogue).").RequireAuthorization(Policies.StaffAdmin);
        admin.MapGet("/queues/refunds", async (int? limit, ISender sender, CancellationToken ct) => (await sender.Send(new GetRefundQueueQuery(limit), ct)).ToHttpResult())
            .WithName("BackOfficeRefundQueue").WithSummary("Staff: refunds awaiting approval; four-eyes above NGN 100,000.").RequireAuthorization(Policies.StaffAdmin);
        admin.MapGet("/queues/disputes", async (int? limit, ISender sender, CancellationToken ct) => (await sender.Send(new GetDisputeQueueQuery(limit), ct)).ToHttpResult())
            .WithName("BackOfficeDisputeQueue").WithSummary("Staff: open payout holds and tutors flagged by strikes.").RequireAuthorization(Policies.StaffAdmin);
        admin.MapGet("/data-requests", async (string? status, int? limit, ISender sender, CancellationToken ct) => (await sender.Send(new GetDataRequestQueueQuery(status, limit), ct)).ToHttpResult())
            .WithName("BackOfficeDataRequests").WithSummary("Staff: NDPA data-subject requests (default: pending).").RequireAuthorization(Policies.StaffAdmin);
    }
}
