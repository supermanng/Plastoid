using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Infrastructure.Security;
using Plastoids.Modules.Identity.Contracts;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Modules.Identity.Services;

/// <summary>Implements <see cref="IUserDirectory"/>; joins the caller's unit of work so cross-module reads share the connection.</summary>
internal sealed class UserDirectory(IdentityDbContext db, IUnitOfWork unitOfWork, IFieldEncryptor encryptor, IClock clock) : IUserDirectory
{
    public async Task<UserSummary?> GetAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }

        var user = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.Id, u.DisplayName, u.Email, u.Status, u.PhoneHash, u.DobCiphertext }).SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        int? age = null;
        if (user.DobCiphertext.Length > 0 && DateOnly.TryParseExact(encryptor.Decrypt(user.DobCiphertext), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dob))
        {
            var today = DateOnly.FromDateTime(clock.UtcNow);
            age = today.Year - dob.Year - (today < dob.AddYears(today.Year - dob.Year) ? 1 : 0);
        }

        return new UserSummary(user.Id, user.DisplayName, user.Email, user.Status == Domain.UserStatus.Active, user.PhoneHash, age);
    }

    public async Task<UserSummary?> FindByContactAsync(string? phone, string? email, CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }

        Guid? id = null;
        if (!string.IsNullOrWhiteSpace(phone) && Domain.PhoneNumber.TryNormalise(phone, out var e164))
        {
            var hash = encryptor.BlindIndex(e164);
            id = await db.Users.AsNoTracking().Where(u => u.PhoneHash == hash).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(cancellationToken);
        }

        if (id is null && !string.IsNullOrWhiteSpace(email))
        {
            var normalised = email.Trim().ToLowerInvariant();
            id = await db.Users.AsNoTracking().Where(u => u.Email == normalised).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(cancellationToken);
        }

        return id is null ? null : await GetAsync(id.Value, cancellationToken);
    }
}

internal sealed class OrganisationDirectory(IdentityDbContext db, IUnitOfWork unitOfWork) : IOrganisationDirectory
{
    public async Task<OrganisationSummary?> GetAsync(Guid organisationId, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        return await db.Organisations.AsNoTracking().Where(o => o.Id == organisationId)
            .Select(o => new OrganisationSummary(o.Id, o.Name, o.Type.ToString().ToLowerInvariant(), o.VerificationStatus == Domain.VerificationStatus.Verified))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> IsMemberAsync(Guid userId, Guid organisationId, IReadOnlyCollection<string>? roles, CancellationToken cancellationToken)
    {
        await EnlistAsync(cancellationToken);
        var query = db.Memberships.AsNoTracking().Where(m => m.UserId == userId && m.OrganisationId == organisationId);
        if (roles is { Count: > 0 })
        {
            query = query.Where(m => roles.Contains(m.Role));
        }

        return await query.AnyAsync(cancellationToken);
    }

    private async Task EnlistAsync(CancellationToken cancellationToken)
    {
        if (unitOfWork.IsActive)
        {
            await unitOfWork.EnlistAsync(db, cancellationToken);
        }
    }
}
