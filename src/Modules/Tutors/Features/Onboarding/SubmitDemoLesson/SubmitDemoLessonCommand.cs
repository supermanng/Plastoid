using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Tutors.Features.Onboarding.SubmitDemoLesson;

/// <summary>Stage 3: registers the 5-minute demo lesson and queues subject review (5 working days). Returns a presigned upload URL.</summary>
public sealed record SubmitDemoLessonCommand(string ContentType) : ICommand<SubmitDemoLessonResponse>;

public sealed record SubmitDemoLessonResponse(Uri UploadUrl, DateTime ReviewDueAt);
