using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Applications.Domain;
using Plastoids.Modules.Applications.Persistence;
using Plastoids.Modules.Applications.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Applications.Features.Applying.FileAppeal;

/// <summary>A rejected applicant may appeal once within 7 days of the decision (blueprint §9.13 appeals).</summary>
public sealed record FileAppealCommand(Guid ApplicationId, string Reason) : ICommand<AppealView>;

public sealed record FileAppealRequest(string Reason);

public sealed record AppealView(Guid AppealId, Guid ApplicationId, string Status, DateTime FiledAt, string? Resolution);

internal sealed class FileAppealValidator : AbstractValidator<FileAppealCommand>
{
    public FileAppealValidator()
    {
        RuleFor(x => x.ApplicationId).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MinimumLength(20).MaximumLength(2000);
    }
}

internal sealed class FileAppealHandler(ApplicationsDbContext db, ApplicationAccess access, IClock clock) : ICommandHandler<FileAppealCommand, AppealView>
{
    public async Task<Result<AppealView>> Handle(FileAppealCommand request, CancellationToken cancellationToken)
    {
        var mine = await access.MineAsync(request.ApplicationId, cancellationToken);
        if (mine.IsFailure)
        {
            return Result.Failure<AppealView>(mine.Error);
        }

        var application = mine.Value;
        var now = clock.UtcNow;
        if (!application.CanAppeal(now))
        {
            return Error.Conflict("appeal_not_allowed", "Only rejected applications can be appealed, within 7 days of the decision.");
        }

        if (await db.Appeals.AnyAsync(a => a.ApplicationId == application.Id, cancellationToken))
        {
            return Error.Conflict("appeal_already_filed", "An appeal was already filed for this application.");
        }

        var appeal = Appeal.File(application.TenantId, application, request.Reason, now);
        db.Appeals.Add(appeal);
        return new AppealView(appeal.Id, appeal.ApplicationId, appeal.Status.ToString().ToLowerInvariant(), appeal.FiledAt, appeal.Resolution);
    }
}

internal static class FileAppealEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{applicationId:guid}/appeals", async (Guid applicationId, FileAppealRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new FileAppealCommand(applicationId, body.Reason), ct)).ToCreatedResult(r => $"/api/v1/applications/{r.ApplicationId}/appeals/{r.AppealId}"))
            .WithName("FileAppeal")
            .WithSummary("Learner: appeal a rejection within 7 days.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
