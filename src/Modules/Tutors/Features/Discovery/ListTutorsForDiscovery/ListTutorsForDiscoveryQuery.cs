using Plastoids.Infrastructure.Http;
using Plastoids.SharedKernel.Cqrs;
using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Tutors.Features.Discovery.ListTutorsForDiscovery;

/// <summary>Public discovery: verified tutors only. FULLTEXT on headline/bio plus hard filters (blueprint §13).</summary>
public sealed record ListTutorsForDiscoveryQuery(string? Q, string? Skill, long? MaxRateKobo, string? Lang, string? Format, string? Cursor, int? Limit) : IQuery<Page<TutorCard>>;

public sealed record TutorCard(Guid TutorId, string Headline, string Level, IReadOnlyList<string> Subjects, IReadOnlyList<string> Languages, IReadOnlyList<string> Formats, Money HourlyRate, decimal RatingAverage, int RatingCount, string TimeZone);
