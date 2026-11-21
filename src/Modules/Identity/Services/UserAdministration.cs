using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Identity.Domain;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Results;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Identity.Services;

internal sealed class UserAdministration(IdentityDbContext db, IUnitOfWork unitOfWork, IClock clock) : IUserAdministration
{
    public async Task<IReadOnlyList<UserAdminSummary>> SearchUsersAsync(string? query, string? role, string? status, int limit, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var users = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var q = query.Trim();
            users = Guid.TryParse(q, out var id) ? users.Where(u => u.Id == id) : users.Where(u => u.DisplayName.Contains(q) || (u.Email != null && u.Email.Contains(q)));
        }

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<UserStatus>(status, true, out var parsed))
        {
            users = users.Where(u => u.Status == parsed);
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            var r = role.Trim().ToLowerInvariant();
            users = users.Where(u => db.Memberships.Any(m => m.UserId == u.Id && m.OrganisationId == null && m.Role == r));
        }

        var rows = await users.OrderByDescending(u => u.CreatedAt).Take(Math.Clamp(limit, 1, 200)).ToListAsync(cancellationToken);
        var ids = rows.Select(u => u.Id).ToList();
        var roles = await db.Memberships.AsNoTracking().Where(m => ids.Contains(m.UserId) && m.OrganisationId == null).Select(m => new { m.UserId, m.Role }).ToListAsync(cancellationToken);
        return rows.Select(u => Summary(u, roles.Where(m => m.UserId == u.Id).Select(m => m.Role).ToList())).ToList();
    }

    public async Task<UserAdminSummary?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user is null ? null : Summary(user, await RolesAsync(userId, cancellationToken));
    }

    public async Task<Result<UserAdminSummary>> SuspendAsync(Guid userId, string reason, Guid staffId, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return Error.NotFound("user_not_found", "No such user.");
        }

        if (userId == staffId)
        {
            return Error.Forbidden("self_suspension", "Staff cannot suspend their own account.");
        }

        var suspended = user.Suspend(reason);
        if (suspended.IsFailure)
        {
            return Result.Failure<UserAdminSummary>(suspended.Error);
        }

        // Sessions end now: refresh tokens are revoked so the suspension takes effect at the next token refresh.
        foreach (var token in await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(cancellationToken))
        {
            token.Revoke(clock.UtcNow);
        }

        return Summary(user, await RolesAsync(userId, cancellationToken));
    }

    public async Task<Result<UserAdminSummary>> ReinstateAsync(Guid userId, Guid staffId, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return Error.NotFound("user_not_found", "No such user.");
        }

        var reinstated = user.Reinstate();
        return reinstated.IsFailure ? Result.Failure<UserAdminSummary>(reinstated.Error) : Summary(user, await RolesAsync(userId, cancellationToken));
    }

    public async Task<IReadOnlyList<OrganisationAdminSummary>> ListOrganisationsAsync(string? verificationStatus, int limit, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var query = db.Organisations.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(verificationStatus) && Enum.TryParse<VerificationStatus>(verificationStatus, true, out var parsed))
        {
            query = query.Where(o => o.VerificationStatus == parsed);
        }

        return await query.OrderBy(o => o.CreatedAt).Take(Math.Clamp(limit, 1, 200))
            .Select(o => new OrganisationAdminSummary(o.Id, o.Name, o.Type.ToString().ToLowerInvariant(), o.VerificationStatus.ToString().ToLowerInvariant(), o.CacNumber, o.SignatoryName, o.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DataRequestSummary>> ListDataRequestsAsync(string? status, int limit, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var query = db.DataExportRequests.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<DataExportStatus>(status, true, out var parsed))
        {
            query = query.Where(r => r.Status == parsed);
        }

        return await query.OrderBy(r => r.RequestedAt).Take(Math.Clamp(limit, 1, 200))
            .Select(r => new DataRequestSummary(r.Id, r.UserId, r.Status.ToString().ToLowerInvariant(), r.RequestedAt, r.CompletedAt, r.FailureReason, r.Attempts))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<string>> RolesAsync(Guid userId, CancellationToken ct)
        => await db.Memberships.AsNoTracking().Where(m => m.UserId == userId && m.OrganisationId == null).Select(m => m.Role).ToListAsync(ct);

    private static UserAdminSummary Summary(User u, IReadOnlyList<string> roles)
        => new(u.Id, u.DisplayName, u.Email, u.Status.ToString().ToLowerInvariant(), roles, u.CreatedAt, u.LastLoginAt, u.MfaEnabled);

    private async Task EnlistAsync(CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }
    }
}
