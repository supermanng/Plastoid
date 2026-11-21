using Plastoids.SharedKernel.Cqrs;

namespace Plastoids.Modules.Tutors.Features.Onboarding.ReviewSubject;

public sealed record ReviewSubjectCommand(Guid TutorId, string Decision, int? Score, string? Note, string IdempotencyKey) : ICommand<ReviewSubjectResponse>, IIdempotentCommand;

public sealed record ReviewSubjectRequest(string Decision, int? Score, string? Note);

public sealed record ReviewSubjectResponse(Guid TutorId, string Status, bool Verified);
