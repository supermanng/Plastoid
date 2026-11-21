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

namespace Plastoids.Modules.Applications.Features.Review.BulkDecide;

public sealed record DecisionInput(Guid ApplicationId, string Decision, string Reason);

/// <summary>Story F4: bulk accept / reject / waitlist; every decision is an append-only row with reviewer and reason.</summary>
public sealed record BulkDecideCommand(Guid ProgrammeId, IReadOnlyList<DecisionInput> Decisions, string IdempotencyKey) : ICommand<BulkDecideResponse>, IIdempotentCommand;

public sealed record BulkDecideRequest(IReadOnlyList<DecisionInput> Decisions);

public sealed record DecisionFailure(Guid ApplicationId, string Code, string Message);

public sealed record BulkDecideResponse(Guid ProgrammeId, int Applied, IReadOnlyList<DecisionFailure> Failures);

internal sealed class BulkDecideValidator : AbstractValidator<BulkDecideCommand>
{
    public BulkDecideValidator()
    {
        RuleFor(x => x.ProgrammeId).NotEmpty();
        RuleFor(x => x.Decisions).NotEmpty().Must(d => d.Count <= 500).WithMessage("At most 500 decisions per call.");
        RuleForEach(x => x.Decisions).ChildRules(d =>
        {
            d.RuleFor(x => x.ApplicationId).NotEmpty();
            d.RuleFor(x => x.Decision).Must(DecisionKinds.IsValid).WithMessage("decision must be accepted, rejected or waitlisted.");
            d.RuleFor(x => x.Reason).NotEmpty().MaximumLength(1000);
        });
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(128);
    }
}

internal sealed class BulkDecideHandler(ApplicationsDbContext db, IOutbox outbox, ICurrentUser currentUser, IClock clock) : ICommandHandler<BulkDecideCommand, BulkDecideResponse>
{
    public async Task<Result<BulkDecideResponse>> Handle(BulkDecideCommand request, CancellationToken cancellationToken)
    {
        var ids = request.Decisions.Select(d => d.ApplicationId).Distinct().ToList();
        var applications = await db.Applications.Where(a => a.ProgrammeId == request.ProgrammeId && ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, cancellationToken);
        var reviewer = currentUser.UserId ?? Guid.Empty;
        var now = clock.UtcNow;
        var applied = 0;
        var failures = new List<DecisionFailure>();
        foreach (var input in request.Decisions)
        {
            if (!applications.TryGetValue(input.ApplicationId, out var application))
            {
                failures.Add(new DecisionFailure(input.ApplicationId, "application_not_found", "Not an application on this programme."));
                continue;
            }

            if (application.IsFlagged && input.Decision == DecisionKinds.Accepted)
            {
                failures.Add(new DecisionFailure(input.ApplicationId, "application_flagged", $"Flagged ({application.FlagReason}); resolve the flag before accepting."));
                continue;
            }

            var decided = application.Decide(input.Decision, now);
            if (decided.IsFailure)
            {
                failures.Add(new DecisionFailure(input.ApplicationId, decided.Error.Code, decided.Error.Message));
                continue;
            }

            db.Decisions.Add(Decision.Record(application.TenantId, application, input.Decision, input.Reason, reviewer, "bulk", now));
            outbox.Enqueue(new DecisionRecorded(application.Id, application.Id, application.ProgrammeId, application.LearnerId, input.Decision, reviewer));
            if (input.Decision != DecisionKinds.Accepted)
            {
                outbox.Enqueue(NotificationRequested.To(application.LearnerId, "applications.decision." + input.Decision, new Dictionary<string, string> { ["applicationId"] = application.Id.ToString("D") }));
            }

            applied++;
        }

        return new BulkDecideResponse(request.ProgrammeId, applied, failures);
    }
}

internal static class BulkDecideEndpoint
{
    public static void Map(IEndpointRouteBuilder admin)
    {
        admin.MapPost("/programmes/{programmeId:guid}/decisions", async (Guid programmeId, BulkDecideRequest body, HttpContext http, ISender sender, CancellationToken ct) =>
            {
                var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault() ?? string.Empty;
                return (await sender.Send(new BulkDecideCommand(programmeId, body.Decisions, key), ct)).ToHttpResult();
            })
            .WithName("BulkDecide")
            .WithSummary("Staff: bulk accept / reject / waitlist with a reason each; decisions are append-only. Requires Idempotency-Key.")
            .RequireAuthorization(Policies.StaffAdmin);
    }
}
