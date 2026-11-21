using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Plastoids.SharedKernel.Tenancy;

namespace Plastoids.Infrastructure.Tenancy;

/// <summary>
/// Resolves tenant and user from the HTTP request when there is one, otherwise from an ambient
/// <see cref="AmbientContext"/> set by jobs and consumers. Marketplace is the default tenant (tenant zero).
/// </summary>
internal sealed class CurrentContext(IHttpContextAccessor accessor) : ICurrentTenant, ICurrentUser
{
    public const string TenantClaim = "tenant_id";
    public const string UserIdClaim = "sub";
    public const string OrganisationClaim = "org_id";
    public const string CorrelationHeader = "X-Correlation-Id";

    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public Guid TenantId
    {
        get
        {
            if (AmbientContext.Current is { } ambient)
            {
                return ambient.TenantId;
            }

            var claim = Principal?.FindFirst(TenantClaim)?.Value;
            return Guid.TryParse(claim, out var id) ? id : Tenants.Marketplace;
        }
    }

    public bool IsSystemContext => AmbientContext.Current?.IsSystem ?? Principal?.IsInRole(RoleNames.System) ?? false;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId
    {
        get
        {
            if (AmbientContext.Current is { } ambient)
            {
                return ambient.ActorId;
            }

            var claim = Principal?.FindFirst(UserIdClaim)?.Value ?? Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }


    public IReadOnlyCollection<string> Roles
        => Principal?.Identities.SelectMany(i => i.FindAll(i.RoleClaimType)).Select(c => c.Value).Distinct().ToArray() ?? [];

    public bool MfaVerified => Principal?.HasClaim("amr", "mfa") ?? false;

    public IReadOnlyCollection<Guid> OrganisationIds
        => Principal?.FindAll(OrganisationClaim).Select(c => Guid.TryParse(c.Value, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToArray() ?? [];

    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? CorrelationId
        => AmbientContext.Current?.CorrelationId
           ?? accessor.HttpContext?.Request.Headers[CorrelationHeader].FirstOrDefault()
           ?? accessor.HttpContext?.TraceIdentifier;
}

public static class RoleNames
{
    public const string Learner = "learner";
    public const string Tutor = "tutor";
    public const string Sponsor = "sponsor";
    public const string Staff = "staff";
    public const string System = "system";
}

/// <summary>Ambient tenant/actor for non-HTTP work (Kafka consumers, Quartz jobs). Dispose the scope to restore.</summary>
public sealed record AmbientContext(Guid TenantId, Guid? ActorId, string? CorrelationId, bool IsSystem)
{
    private static readonly AsyncLocal<AmbientContext?> Holder = new();

    public static AmbientContext? Current => Holder.Value;

    public static IDisposable Enter(Guid tenantId, Guid? actorId = null, string? correlationId = null, bool isSystem = false)
    {
        var previous = Holder.Value;
        Holder.Value = new AmbientContext(tenantId, actorId, correlationId, isSystem);
        return new Restore(previous);
    }

    private sealed class Restore(AmbientContext? previous) : IDisposable
    {
        public void Dispose() => Holder.Value = previous;
    }
}
