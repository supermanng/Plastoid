using System.Collections.Concurrent;
using Plastoids.Modules.Catalogue.Contracts;

namespace Plastoids.Modules.Catalogue.Services;

/// <summary>Until the Lms module exists (Sprint 5) nobody is enrolled; tests register snapshots directly.</summary>
public sealed class StubEnrolmentProgress : IEnrolmentProgress
{
    public ConcurrentDictionary<Guid, EnrolmentProgressSnapshot> Snapshots { get; } = new();

    public Task<EnrolmentProgressSnapshot?> GetAsync(Guid enrolmentId, Guid learnerId, Guid courseId, CancellationToken cancellationToken)
        => Task.FromResult(Snapshots.TryGetValue(enrolmentId, out var s) && s.LearnerId == learnerId && s.CourseId == courseId ? s : null);
}
