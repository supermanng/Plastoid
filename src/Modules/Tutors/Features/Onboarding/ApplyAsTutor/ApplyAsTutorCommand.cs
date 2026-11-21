using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Tutors.Features.Onboarding.ApplyAsTutor;

public sealed record ApplyAsTutorCommand(
    string Headline,
    string Bio,
    IReadOnlyList<string> Subjects,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> Formats,
    string TimeZone,
    Money HourlyRate,
    bool AcceptCodeOfConduct) : ICommand<ApplyAsTutorResponse>;

public sealed record ApplyAsTutorResponse(Guid TutorId, string Status);
