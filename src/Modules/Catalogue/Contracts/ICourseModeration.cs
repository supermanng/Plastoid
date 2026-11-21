namespace Plastoids.Modules.Catalogue.Contracts;

public sealed record CourseModerationItem(Guid CourseId, string Title, string Slug, Guid OwnerUserId, string Status, DateTime? SubmittedAt, string? ReviewNote, decimal RatingAverage, int RatingCount);

/// <summary>Back-office moderation queue (read-only). Decisions stay on POST /admin/catalogue/courses/{id}/review so there is one review path.</summary>
public interface ICourseModeration
{
    Task<IReadOnlyList<CourseModerationItem>> ListAsync(string? status, int limit, CancellationToken cancellationToken);
}
