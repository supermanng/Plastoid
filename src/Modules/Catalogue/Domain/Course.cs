using System.Text.RegularExpressions;
using Plastoids.SharedKernel.Domain;
using Plastoids.SharedKernel.Results;

namespace Plastoids.Modules.Catalogue.Domain;

public enum CourseStatus
{
    Draft = 0,
    InReview = 1,
    Approved = 2,
    Published = 3,
    Unpublished = 4,
    Rejected = 5,
}

public enum CourseOwnerType
{
    Tutor = 0,
    Tenant = 1,
    Plastoids = 2,

    /// <summary>Owned by a verified organisation; OwnerId / OrganisationId hold the organisation, OwnerUserId the member who created it (ADR 0019).</summary>
    Organisation = 3,
}

public static class CourseLevels
{
    public const string Beginner = "beginner";
    public const string Intermediate = "intermediate";
    public const string Advanced = "advanced";
    public static readonly IReadOnlyCollection<string> All = [Beginner, Intermediate, Advanced];
    public static bool IsValid(string level) => All.Contains(level, StringComparer.Ordinal);
}

/// <summary>
/// Blueprint §16 catalogue.Course. Authoring happens on the draft modules/lessons; Publish snapshots them into an
/// immutable CourseVersion. First three courses of an owner need a human review, later ones are sampled.
/// </summary>
[Audited]
public sealed partial class Course : Entity
{
    public const int HumanReviewFirstCourses = 3;
    public const int SampleEvery = 5;

    private Course()
    {
    }

    private Course(Guid id, Guid tenantId) : base(id, tenantId)
    {
    }

    public CourseOwnerType OwnerType { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid OwnerUserId { get; private set; }

    /// <summary>Set (= OwnerId) for organisation-owned courses (ADR 0019); null otherwise.</summary>
    public Guid? OrganisationId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string Summary { get; private set; } = string.Empty;
    public string Level { get; private set; } = CourseLevels.Beginner;
    public string Language { get; private set; } = "en";
    public string Category { get; private set; } = string.Empty;
    public List<string> Skills { get; private set; } = [];
    public CourseStatus Status { get; private set; }
    public int CurrentVersionNo { get; private set; }
    public DateTime? PublishedAt { get; private set; }
    public DateTime? SubmittedAt { get; private set; }
    public string? ReviewNote { get; private set; }
    public decimal RatingAverage { get; private set; }
    public int RatingCount { get; private set; }
    public int EstimatedMinutes { get; private set; }

    public static Course Create(Guid tenantId, CourseOwnerType ownerType, Guid ownerId, Guid ownerUserId, string title, string summary, string level, string language, string category, IEnumerable<string> skills, string slugSuffix)
        => new(NewId(), tenantId)
        {
            OwnerType = ownerType,
            OwnerId = ownerId,
            OwnerUserId = ownerUserId,
            Title = title.Trim(),
            Slug = Slugify(title) + "-" + slugSuffix,
            Summary = summary.Trim(),
            Level = level,
            Language = language.Trim().ToLowerInvariant(),
            Category = category.Trim().ToLowerInvariant(),
            Skills = NormaliseSkills(skills),
            Status = CourseStatus.Draft,
        };

    /// <summary>An organisation course (ADR 0019): the organisation is the owner, <paramref name="createdByUserId"/> the authoring member.</summary>
    public static Course CreateForOrganisation(Guid tenantId, Guid organisationId, Guid createdByUserId, string title, string summary, string level, string language, string category, IEnumerable<string> skills, string slugSuffix)
    {
        var course = Create(tenantId, CourseOwnerType.Organisation, organisationId, createdByUserId, title, summary, level, language, category, skills, slugSuffix);
        course.OrganisationId = organisationId;
        return course;
    }

    public bool IsOrganisationOwned => OwnerType == CourseOwnerType.Organisation;

    /// <summary>Lower-case owner kind as other modules see it (<see cref="Contracts.CourseOwnerTypes"/>).</summary>
    public string OwnerTypeName => OwnerType.ToString().ToLowerInvariant();

    public bool IsEditable => Status is CourseStatus.Draft or CourseStatus.Rejected or CourseStatus.Approved or CourseStatus.Published or CourseStatus.Unpublished;

    public Result UpdateOutline(string? title, string? summary, string? level, string? language, string? category, IEnumerable<string>? skills)
    {
        if (Status == CourseStatus.InReview)
        {
            return Error.Conflict("course_in_review", "The course is being reviewed; edits reopen after the decision.");
        }

        if (!string.IsNullOrWhiteSpace(title))
        {
            Title = title.Trim();
        }

        if (summary is not null)
        {
            Summary = summary.Trim();
        }

        if (level is not null)
        {
            Level = level;
        }

        if (!string.IsNullOrWhiteSpace(language))
        {
            Language = language.Trim().ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            Category = category.Trim().ToLowerInvariant();
        }

        if (skills is not null)
        {
            Skills = NormaliseSkills(skills);
        }

        // Any edit after approval invalidates the approval: the next publish needs a fresh review.
        if (Status == CourseStatus.Approved)
        {
            Status = CourseStatus.Draft;
        }

        return Result.Success();
    }

    public Result Submit(DateTime utcNow)
    {
        if (Status == CourseStatus.InReview)
        {
            return Error.Conflict("course_in_review", "Already submitted.");
        }

        Status = CourseStatus.InReview;
        SubmittedAt = utcNow;
        ReviewNote = null;
        return Result.Success();
    }

    public Result RecordReview(bool approved, string? note)
    {
        if (Status != CourseStatus.InReview)
        {
            return Error.Conflict("course_not_in_review", "The course is not awaiting review.");
        }

        Status = approved ? CourseStatus.Approved : CourseStatus.Rejected;
        ReviewNote = note?.Trim();
        return Result.Success();
    }

    /// <summary>Creates the next version number. The caller snapshots the outline into a CourseVersion.</summary>
    public Result<int> Publish(bool ownerVerified, DateTime utcNow)
    {
        if (!ownerVerified)
        {
            return OwnerNotVerified();
        }

        if (Status != CourseStatus.Approved)
        {
            return Error.Conflict("course_not_approved", "Submit the course for review and wait for approval before publishing.");
        }

        CurrentVersionNo++;
        Status = CourseStatus.Published;
        PublishedAt = utcNow;
        return CurrentVersionNo;
    }

    /// <summary>The error a publish or submit gets when the owner has not passed verification: tutor_not_verified or organisation_not_verified.</summary>
    public Error OwnerNotVerified()
        => IsOrganisationOwned
            ? Error.Forbidden("organisation_not_verified", "The organisation must be verified (CAC and signatory) before it can publish courses.")
            : Error.Forbidden("tutor_not_verified", "Tutors can publish only after identity and subject review pass.");

    public void Unpublish() => Status = CourseStatus.Unpublished;

    public void SetEstimatedMinutes(int minutes) => EstimatedMinutes = Math.Max(0, minutes);

    public void RecordRating(int rating)
    {
        RatingAverage = Math.Round((RatingAverage * RatingCount + rating) / (RatingCount + 1), 2);
        RatingCount++;
    }

    /// <summary>Human review for the owner's first three courses, then one in five (deterministic on the course id).</summary>
    public static bool RequiresHumanReview(int ownerPublishedOrReviewedCourses, Guid courseId)
        => ownerPublishedOrReviewedCourses < HumanReviewFirstCourses || (courseId.GetHashCode() & 0x7FFFFFFF) % SampleEvery == 0;

    public static string Slugify(string title)
    {
        var lower = title.Trim().ToLowerInvariant();
        var slug = NonSlug().Replace(lower, "-").Trim('-');
        return slug.Length > 60 ? slug[..60].TrimEnd('-') : slug;
    }

    public static List<string> NormaliseSkills(IEnumerable<string> skills)
        => skills.Select(s => s.Trim().ToLowerInvariant()).Where(s => s.Length > 0).Distinct().Take(15).ToList();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlug();
}
