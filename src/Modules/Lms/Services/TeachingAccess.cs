using Plastoids.Modules.Catalogue.Contracts;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Lms.Services;

/// <summary>
/// ADR 0019 §4 teaching access: the course owner, staff, an owner/author of the owning organisation, or a tutor with an active
/// engagement on the course may manage its cohorts, sessions, gradebook and grading. Cohort-level checks add the cohort's tutor.
/// </summary>
internal sealed class TeachingAccess(ICurrentUser currentUser, IOrganisationDirectory organisations, IEngagementReader engagements)
{
    /// <summary>Organisation roles that manage the organisation's courses (Identity's OrganisationRoles.Owner / Author).</summary>
    private static readonly IReadOnlyCollection<string> ManagingRoles = ["owner", "author"];

    public async Task<bool> CanManageCourseAsync(PublishedCourse course, Guid userId, CancellationToken cancellationToken)
        => await OwnsCourseAsync(course, userId, cancellationToken) || await engagements.IsTeachingAsync(course.CourseId, userId, cancellationToken);

    /// <summary>Owner-side management only (no engaged-tutor rule): the course owner, staff, or an owner/author of the owning organisation.</summary>
    public async Task<bool> OwnsCourseAsync(PublishedCourse course, Guid userId, CancellationToken cancellationToken)
    {
        if (course.OwnerUserId == userId || currentUser.Roles.Contains("staff"))
        {
            return true;
        }

        return course.OrganisationId is { } organisationId && await organisations.IsMemberAsync(userId, organisationId, ManagingRoles, cancellationToken);
    }

    public Task<bool> IsEngagedAsync(PublishedCourse course, Guid userId, CancellationToken cancellationToken)
        => engagements.IsTeachingAsync(course.CourseId, userId, cancellationToken);
}
