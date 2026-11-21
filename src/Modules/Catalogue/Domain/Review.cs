using Plastoids.SharedKernel.Domain;

namespace Plastoids.Modules.Catalogue.Domain;

/// <summary>Verified-learner review (blueprint §9.2 rules enforced by the PostReview slice).</summary>
public sealed class Review : Entity
{
    private Review()
    {
    }

    private Review(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public Guid CourseId { get; private set; }
    public Guid LearnerId { get; private set; }
    public Guid EnrolmentId { get; private set; }
    public int Rating { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public decimal ProgressAtReview { get; private set; }

    public static Review Create(Guid tenantId, Guid courseId, Guid learnerId, Guid enrolmentId, int rating, string body, decimal progress)
        => new(NewId(), tenantId) { CourseId = courseId, LearnerId = learnerId, EnrolmentId = enrolmentId, Rating = rating, Body = body.Trim(), ProgressAtReview = progress };
}
