using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Catalogue.Domain;
using Plastoids.Modules.Catalogue.Persistence;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Catalogue.Services;

/// <summary>
/// Loads a course the caller may author (ADR 0019 §2): the owning tutor, an owner/author of the owning organisation, or staff.
/// Ownership is enforced here; the endpoints only require an MFA session.
/// </summary>
internal sealed class CourseAccess(CatalogueDbContext db, ICurrentUser currentUser, IOrganisationDirectory organisations)
{
    /// <summary>Organisation roles that may author courses and engage tutors (Identity's OrganisationRoles.Owner / Author).</summary>
    public static readonly IReadOnlyCollection<string> AuthoringRoles = ["owner", "author"];

    public async Task<Result<Course>> OwnedAsync(Guid courseId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } userId)
        {
            return Error.Unauthorized("not_signed_in", "Sign in first.");
        }

        var course = await db.Courses.SingleOrDefaultAsync(c => c.Id == courseId, cancellationToken);
        if (course is null)
        {
            return Error.NotFound("course_not_found", "No such course.");
        }

        if (!await CanAuthorAsync(course, userId, cancellationToken))
        {
            return Error.Forbidden("not_course_owner", "You do not own this course.");
        }

        return course;
    }

    /// <summary>True when <paramref name="userId"/> may author <paramref name="course"/>: its owner, staff, or an owner/author of the owning organisation.</summary>
    public async Task<bool> CanAuthorAsync(Course course, Guid userId, CancellationToken cancellationToken)
    {
        if (course.OwnerUserId == userId || currentUser.Roles.Contains("staff"))
        {
            return true;
        }

        return course.IsOrganisationOwned && course.OrganisationId is { } organisationId
            && await organisations.IsMemberAsync(userId, organisationId, AuthoringRoles, cancellationToken);
    }

    /// <summary>Tutor courses: the owner is in the VerifiedTutors projection. Organisation courses: the organisation is verified. Platform courses: always.</summary>
    public async Task<bool> OwnerIsVerifiedAsync(Course course, CancellationToken cancellationToken)
        => course.OwnerType switch
        {
            CourseOwnerType.Tutor => await db.VerifiedTutors.AnyAsync(t => t.UserId == course.OwnerUserId, cancellationToken),
            CourseOwnerType.Organisation => course.OrganisationId is { } organisationId && (await organisations.GetAsync(organisationId, cancellationToken))?.IsVerified == true,
            _ => true,
        };
}
