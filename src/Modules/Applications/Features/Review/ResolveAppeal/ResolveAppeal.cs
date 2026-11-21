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
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Messaging;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Applications.Features.Review.ResolveAppeal;

/// <summary>Upheld appeals return the application to the pool (waitlisted) with an appeal-sourced decision row.</summary>
public sealed record ResolveAppealCommand(Guid AppealId, string Decision, string Reason, string IdempotencyKey) : ICommand<ResolveAppealResponse>, IIdempotentCommand;

public sealed record ResolveAppealRequest(string Decision, string Reason);

public sealed record ResolveAppealResponse(Guid AppealId, Guid ApplicationId, string AppealStatus, string ApplicationStatus);

internal sealed class ResolveAppealValidator : AbstractValidator<ResolveAppealCommand>
{
    public ResolveAppealValidator()
    {
        RuleFor(x => x.AppealId).NotEmpty();
        RuleFor(x => x.Decision).Must(d => d is "upheld" or "dismissed").WithMessage("decision must be upheld or dismissed.");
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class ResolveAppealHandler(ApplicationsDbContext db, IOutbox outbox, ICurrentUser currentUser, IClock clock) : ICommandHandler<ResolveAppealCommand, ResolveAppealResponse>
{
    public async Task<Result<ResolveAppealResponse>> Handle(ResolveAppealCommand request, CancellationToken cancellationToken)
    {
        var appeal = await db.Appeals.SingleOrDefaultAsync(a => a.Id == request.AppealId, cancellationToken);
        if (appeal is null)
        {
            return Error.NotFound("appeal_not_found", "No such appeal.");
        }

        var application = await db.Applications.SingleAsync(a => a.Id == appeal.ApplicationId, cancellationToken);
        var staffId = currentUser.UserId ?? Guid.Empty;
        var now = clock.UtcNow;
        var upheld = request.Decision == "upheld";
        var resolved = appeal.Resolve(upheld, request.Reason, staffId, now);
        if (resolved.IsFailure)
        {
            return Result.Failure<ResolveAppealResponse>(resolved.Error);
        }

        if (upheld)
        {
            var decided = application.Decide(DecisionKinds.Waitlisted, now);
            if (decided.IsFailure)
            {
                return Result.Failure<ResolveAppealResponse>(decided.Error);
            }

            db.Decisions.Add(Decision.Record(application.TenantId, application, DecisionKinds.Waitlisted, "Appeal upheld: " + request.Reason, staffId, "appeal", now));
            outbox.Enqueue(new DecisionRecorded(application.Id, application.Id, application.ProgrammeId, application.LearnerId, DecisionKinds.Waitlisted, staffId));
        }

        outbox.Enqueue(NotificationRequested.To(application.LearnerId, "applications.appeal." + request.Decision, new Dictionary<string, string> { ["applicationId"] = application.Id.ToString("D"), ["reason"] = request.Reason }));
        return new ResolveAppealResponse(appeal.Id, application.Id, appeal.Status.ToString().ToLowerInvariant(), application.Status.ToString().ToLowerInvariant());
    }
}

internal static class ResolveAppealEndpoint
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapPost("/appeals/{appealId:guid}/resolve", async (Guid appealId, ResolveAppealRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new ResolveAppealCommand(appealId, body.Decision, body.Reason, key), ct)).ToHttpResult();
            })
            .WithName("ResolveAppeal")
            .WithSummary("Staff: uphold (back to the waitlist) or dismiss an appeal. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
