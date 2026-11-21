using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Plastoids.Infrastructure.Security;
using Plastoids.Infrastructure.Storage;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.Modules.Identity.Domain;
using Plastoids.Modules.Identity.Persistence;
using Plastoids.SharedKernel.Tenancy;
using Plastoids.SharedKernel.Time;
using Quartz;

namespace Plastoids.Modules.Identity.Jobs;

/// <summary>
/// Story A3: fulfils pending data-export requests (JSON + PDF to blob storage). Polls every 15 s, well inside the
/// 24 h SLA; up to three attempts per request before it is marked failed.
/// </summary>
[DisallowConcurrentExecution]
public sealed class DataExportJob(IdentityDbContext db, IBlobStorage storage, IFieldEncryptor encryptor, IClock clock, ILogger<DataExportJob> logger) : IJob
{
    public const string JobName = "identity-data-export";
    public const int MaxAttempts = 3;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public async Task Execute(IJobExecutionContext context)
    {
        var ct = context.CancellationToken;
        using var ambient = AmbientContext.Enter(Tenants.Marketplace, isSystem: true);
        var pending = await db.DataExportRequests
            .Where(r => r.Status == DataExportStatus.Pending)
            .OrderBy(r => r.RequestedAt)
            .Take(5)
            .ToListAsync(ct);

        foreach (var request in pending)
        {
            try
            {
                await FulfilAsync(request, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Data export {ExportId} failed (attempt {Attempt})", request.Id, request.Attempts + 1);
                request.RecordFailure(ex.Message, MaxAttempts, clock.UtcNow);
            }

            await db.SaveChangesAsync(ct);
        }
    }

    private async Task FulfilAsync(DataExportRequest request, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == request.UserId, ct);
        var memberships = await db.Memberships.AsNoTracking().Where(m => m.UserId == user.Id).ToListAsync(ct);
        var organisationIds = memberships.Where(m => m.OrganisationId is not null).Select(m => m.OrganisationId!.Value).ToList();
        var organisations = await db.Organisations.AsNoTracking().Where(o => organisationIds.Contains(o.Id)).ToListAsync(ct);
        var exports = await db.DataExportRequests.AsNoTracking().Where(r => r.UserId == user.Id).OrderBy(r => r.RequestedAt).ToListAsync(ct);

        var document = new
        {
            exportedAt = clock.UtcNow,
            account = new
            {
                id = user.Id,
                phone = user.PhoneCiphertext.Length > 0 ? encryptor.Decrypt(user.PhoneCiphertext) : null,
                email = user.Email,
                emailVerifiedAt = user.EmailVerifiedAt,
                displayName = user.DisplayName,
                dateOfBirth = user.DobCiphertext.Length > 0 ? encryptor.Decrypt(user.DobCiphertext) : null,
                mfaEnabled = user.MfaEnabled,
                status = user.Status.ToString(),
                ninVerifiedAt = user.NinVerifiedAt,
                careerGoal = user.CareerGoal,
                interests = user.Interests,
                createdAt = user.CreatedAt,
                lastLoginAt = user.LastLoginAt,
            },
            roles = memberships.Where(m => m.IsPlatformRole).Select(m => m.Role),
            organisations = memberships.Where(m => m.OrganisationId is not null)
                .Join(organisations, m => m.OrganisationId, o => o.Id, (m, o) => new { o.Id, o.Name, type = o.Type.ToString(), m.Role, status = o.VerificationStatus.ToString() }),
            dataExports = exports.Select(e => new { e.Id, status = e.Status.ToString(), e.RequestedAt, e.CompletedAt }),
        };

        var json = JsonSerializer.SerializeToUtf8Bytes(document, Json);
        var lines = new List<string>
        {
            $"Exported at: {clock.UtcNow:u}",
            $"Account id: {user.Id}",
            $"Display name: {user.DisplayName}",
            $"Phone: {(user.PhoneCiphertext.Length > 0 ? encryptor.Decrypt(user.PhoneCiphertext) : "-")}",
            $"Email: {user.Email ?? "-"}",
            $"Date of birth: {(user.DobCiphertext.Length > 0 ? encryptor.Decrypt(user.DobCiphertext) : "-")}",
            $"MFA enabled: {user.MfaEnabled}",
            $"Status: {user.Status}",
            $"Career goal: {user.CareerGoal ?? "-"}",
            $"Interests: {(user.Interests.Count == 0 ? "-" : string.Join(", ", user.Interests))}",
            $"Roles: {string.Join(", ", memberships.Where(m => m.IsPlatformRole).Select(m => m.Role))}",
            string.Empty,
            "Organisations:",
        };
        lines.AddRange(memberships.Where(m => m.OrganisationId is not null)
            .Join(organisations, m => m.OrganisationId, o => o.Id, (m, o) => $"  - {o.Name} ({o.Type}) as {m.Role}, {o.VerificationStatus}"));
        lines.Add(string.Empty);
        lines.Add("Learning, payment and ledger records will be added to this export by their modules in later sprints.");
        var pdf = MinimalPdf.FromLines("Plastoids personal data export", lines);

        var prefix = $"exports/{user.Id:N}/{request.Id:N}";
        await using (var jsonStream = new MemoryStream(json))
        {
            await storage.PutAsync(prefix + ".json", jsonStream, "application/json", ct);
        }

        await using (var pdfStream = new MemoryStream(pdf))
        {
            await storage.PutAsync(prefix + ".pdf", pdfStream, "application/pdf", ct);
        }

        request.Complete(prefix + ".json", prefix + ".pdf", clock.UtcNow);
        logger.LogInformation("Data export {ExportId} for user {UserId} completed ({Bytes} bytes JSON)", request.Id, user.Id, json.Length);
    }
}
