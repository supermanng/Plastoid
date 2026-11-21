using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Lms.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Lms.Features.Learning.CompleteEnrolment;

/// <summary>Re-evaluates the completion rule on demand (it also runs after every progress, quiz and grading change).</summary>
public sealed record CompleteEnrolmentCommand(Guid EnrolmentId) : ICommand<CompleteEnrolmentResponse>;

public sealed record CompleteEnrolmentResponse(Guid EnrolmentId, string Status, decimal ProgressPct, decimal? FinalScore, bool CompletedNow);

internal sealed class CompleteEnrolmentValidator : AbstractValidator<CompleteEnrolmentCommand>
{
    public CompleteEnrolmentValidator()
    {
        RuleFor(x => x.EnrolmentId).NotEmpty();
    }
}

internal sealed class CompleteEnrolmentHandler(LmsAccess access, ProgressService progress) : ICommandHandler<CompleteEnrolmentCommand, CompleteEnrolmentResponse>
{
    public async Task<Result<CompleteEnrolmentResponse>> Handle(CompleteEnrolmentCommand request, CancellationToken cancellationToken)
    {
        var mine = await access.MyEnrolmentAsync(request.EnrolmentId, cancellationToken, requireActive: false);
        if (mine.IsFailure)
        {
            return Result.Failure<CompleteEnrolmentResponse>(mine.Error);
        }

        var e = mine.Value;
        var completedNow = false;
        if (e.IsActive)
        {
            var result = await progress.RecomputeAsync(e, cancellationToken);
            if (result.IsFailure)
            {
                return Result.Failure<CompleteEnrolmentResponse>(result.Error);
            }

            completedNow = result.Value;
        }

        if (e.Status != Domain.EnrolmentStatus.Completed)
        {
            return Error.Validation("completion_rule_not_met", $"{e.CompletedLessons}/{e.RequiredLessons} required lessons complete and every quiz/assignment must pass.");
        }

        return new CompleteEnrolmentResponse(e.Id, e.Status.ToString().ToLowerInvariant(), e.ProgressPct, e.FinalScore, completedNow);
    }
}

internal static class CompleteEnrolmentEndpoint
{
    public static void Map(IEndpointRouteBuilder group)
    {
        group.MapPost("/{enrolmentId:guid}/complete", async (Guid enrolmentId, ISender sender, CancellationToken ct) => (await sender.Send(new CompleteEnrolmentCommand(enrolmentId), ct)).ToHttpResult())
            .WithName("CompleteEnrolment")
            .WithSummary("Apply the completion rule (all required lessons + pass marks); emits lms.enrolment-completed.v1 once.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
