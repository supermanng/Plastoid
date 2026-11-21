using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Modules.Identity.Authorization;

public static class IdentityPolicies
{
    /// <summary>Caller is an owner of the organisation in the route's {organisationId}.</summary>
    public const string OrganisationOwner = "organisation-owner";

    /// <summary>Caller holds any role in the organisation in the route's {organisationId}.</summary>
    public const string OrganisationMember = "organisation-member";
}

/// <summary>Build prompt §2.10: ownership is evaluated in an IAuthorizationHandler per resource type, not in handlers.</summary>
public sealed class OrganisationRoleRequirement(IReadOnlyCollection<string> roles) : IAuthorizationRequirement
{
    public IReadOnlyCollection<string> Roles { get; } = roles;
}

internal sealed class OrganisationRoleHandler(IHttpContextAccessor accessor, IdentityDbContext db, ICurrentUser currentUser)
    : AuthorizationHandler<OrganisationRoleRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, OrganisationRoleRequirement requirement)
    {
        var routeValue = accessor.HttpContext?.GetRouteValue("organisationId")?.ToString();
        if (currentUser.UserId is not { } userId || !Guid.TryParse(routeValue, out var organisationId))
        {
            return;
        }

        var allowed = await db.Memberships.AnyAsync(
            m => m.UserId == userId && m.OrganisationId == organisationId && requirement.Roles.Contains(m.Role),
            accessor.HttpContext?.RequestAborted ?? CancellationToken.None);
        if (allowed)
        {
            context.Succeed(requirement);
        }
    }
}
