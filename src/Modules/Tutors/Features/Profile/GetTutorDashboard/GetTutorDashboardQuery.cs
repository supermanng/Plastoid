using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Tutors.Features.Profile.GetTutorDashboard;

public sealed record GetTutorDashboardQuery : IQuery<TutorDashboard>;

public sealed record VerificationStage(string Name, string Status, DateTime? CompletedAt, DateTime? ReviewDueAt);

public sealed record CredentialSummary(Guid Id, string Type, string Title, string Issuer, string Status, string? ReviewNote);

public sealed record TutorDashboard(
    Guid TutorId,
    string Status,
    string Level,
    string Headline,
    IReadOnlyList<string> Subjects,
    IReadOnlyList<string> Formats,
    string TimeZone,
    Money HourlyRate,
    IReadOnlyList<VerificationStage> Verification,
    IReadOnlyList<CredentialSummary> Credentials,
    int AvailabilityRules,
    int PriceRules,
    bool CalendarConnected,
    string? PayoutAccountMasked,
    decimal QualityScore,
    int SessionsDelivered,
    int EnrolmentsTaught,
    decimal RatingAverage);
