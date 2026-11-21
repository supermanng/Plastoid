using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Security;
using Plastoids.Infrastructure.Storage;
using Plastoids.Modules.Lms.Domain;
using Plastoids.Modules.Lms.Persistence;
using Plastoids.Modules.Lms.Services;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Lms.Features.Learning.SubmitAssignment;

/// <summary>Registers a submission and returns a presigned upload URL for the file (story E1).</summary>
public sealed record SubmitAssignmentCommand(Guid EnrolmentId, Guid LessonId, string ContentType) : ICommand<SubmitAssignmentResponse>;

public sealed record SubmitAssignmentRequest(string ContentType);

public sealed record SubmitAssignmentResponse(Guid SubmissionId, Uri UploadUrl, DateTime SubmittedAt);

internal sealed class SubmitAssignmentValidator : AbstractValidator<SubmitAssignmentCommand>
{
    private static readonly string[] Allowed = ["application/pdf", "application/zip", "image/png", "image/jpeg", "text/plain", "application/vnd.openxmlformats-officedocument.wordprocessingml.document"];

    public SubmitAssignmentValidator()
    {
        RuleFor(x => x.EnrolmentId).NotEmpty();
        RuleFor(x => x.LessonId).NotEmpty();
        RuleFor(x => x.ContentType).Must(c => Allowed.Contains(c)).WithMessage("Upload a PDF, ZIP, image, text or Word file.");
    }
}

internal sealed class SubmitAssignmentHandler(LmsDbContext db, LmsAccess access, ProgressService progress, IBlobStorage storage, IClock clock) : ICommandHandler<SubmitAssignmentCommand, SubmitAssignmentResponse>
{
    public async Task<Result<SubmitAssignmentResponse>> Handle(SubmitAssignmentCommand request, CancellationToken cancellationToken)
    {
        var mine = await access.MyEnrolmentAsync(request.EnrolmentId, cancellationToken);
        if (mine.IsFailure)
        {
            return Result.Failure<SubmitAssignmentResponse>(mine.Error);
        }

        var course = await progress.CourseForAsync(mine.Value, cancellationToken);
        if (course.IsFailure)
        {
            return Result.Failure<SubmitAssignmentResponse>(course.Error);
        }

        if (!course.Value.Lessons.Any(l => l.LessonId == request.LessonId && l.Type == "assignment"))
        {
            return Error.NotFound("assignment_not_found", "No assignment with that lesson id in this course.");
        }

        var now = clock.UtcNow;
        var extension = request.ContentType switch { "application/pdf" => "pdf", "application/zip" => "zip", "image/png" => "png", "image/jpeg" => "jpg", "text/plain" => "txt", _ => "docx" };
        var submissionId = Plastoids.SharedKernel.Domain.Entity.NewId();
        var key = $"assignments/{mine.Value.Id:N}/{request.LessonId:N}/{submissionId:N}.{extension}";
        db.Submissions.Add(AssignmentSubmission.Create(mine.Value.TenantId, mine.Value.Id, request.LessonId, key, request.ContentType, now));
        var upload = await storage.GetPresignedUploadUrlAsync(key, request.ContentType, TimeSpan.FromHours(1), cancellationToken);
        return new SubmitAssignmentResponse(db.Submissions.Local.Last().Id, upload, now);
    }
}

internal static class SubmitAssignmentEndpoint
{
    public static void Map(IEndpointRouteBuilder lms)
    {
        lms.MapPost("/enrolments/{enrolmentId:guid}/lessons/{lessonId:guid}/assignment", async (Guid enrolmentId, Guid lessonId, SubmitAssignmentRequest body, ISender sender, CancellationToken ct) =>
                (await sender.Send(new SubmitAssignmentCommand(enrolmentId, lessonId, body.ContentType), ct)).ToHttpResult(StatusCodes.Status201Created))
            .WithName("SubmitAssignment")
            .WithSummary("Submit an assignment: returns a presigned upload URL for the file.")
            .RequireAuthorization(Policies.Authenticated);
    }
}
