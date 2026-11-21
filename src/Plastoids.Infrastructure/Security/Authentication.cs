using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Plastoids.Infrastructure.Secrets;
using Plastoids.Infrastructure.Tenancy;
using Plastoids.SharedKernel.Time;

namespace Plastoids.Infrastructure.Security;

/// <summary>Claim names used in Plastoids access tokens (short, OIDC-style).</summary>
public static class PlastoidsClaims
{
    public const string Subject = "sub";
    public const string TenantId = "tenant_id";
    public const string Role = "role";
    public const string OrganisationId = "org_id";
    public const string Amr = "amr";
    public const string Scope = "scope";
    public const string TokenUse = "token_use";

    public const string AmrPassword = "pwd";
    public const string AmrOtp = "otp";
    public const string AmrMfa = "mfa";

    public const string UseAccess = "access";
    public const string UseMfaChallenge = "mfa_challenge";
}

/// <summary>Authorization policy names. Resource-level checks (ownership) live in per-resource IAuthorizationHandlers.</summary>
public static class Policies
{
    public const string Authenticated = "authenticated";
    public const string Learner = "learner";
    public const string Tutor = "tutor";
    public const string Sponsor = "sponsor";
    public const string Staff = "staff";

    /// <summary>Any signed-in user whose session completed MFA.</summary>
    public const string Mfa = "mfa";

    /// <summary>Staff role + MFA; the IP allow-list is applied by middleware on /api/v1/admin/*.</summary>
    public const string StaffAdmin = "staff-admin";
}

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public string Issuer { get; set; } = "https://plastoids.ng";
    public string Audience { get; set; } = "plastoids-api";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
    public int MfaChallengeMinutes { get; set; } = 5;
}

/// <summary>What a token is issued for. Roles are platform roles; organisations are the ids the user belongs to.</summary>
public sealed record TokenSubject(
    Guid UserId,
    Guid TenantId,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<Guid> OrganisationIds,
    IReadOnlyCollection<string> Amr);

public sealed record IssuedToken(string Token, DateTime ExpiresAt);

/// <summary>Issues and validates the JWTs this API accepts (ADR 0007: self-issued HS256 until an external IdP exists).</summary>
public interface ITokenService
{
    IssuedToken IssueAccessToken(TokenSubject subject);

    /// <summary>Short-lived token that only authorises completing an MFA challenge.</summary>
    IssuedToken IssueMfaChallenge(Guid userId, Guid tenantId);

    /// <summary>Validates an MFA-challenge token and returns the user id, or null when invalid/expired.</summary>
    Guid? ValidateMfaChallenge(string token);
}

internal sealed class JwtTokenService(IOptions<AuthOptions> options, ISecretProvider secrets, IClock clock) : ITokenService
{
    public const string SigningKeyName = "JwtSigningKey";
    private readonly AuthOptions _options = options.Value;
    private readonly SymmetricSecurityKey _key = new(Encoding.UTF8.GetBytes(secrets.GetRequired(SigningKeyName)));
    private readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };

    public IssuedToken IssueAccessToken(TokenSubject subject)
    {
        var claims = new List<Claim>
        {
            new(PlastoidsClaims.Subject, subject.UserId.ToString("D")),
            new(PlastoidsClaims.TenantId, subject.TenantId.ToString("D")),
            new(PlastoidsClaims.TokenUse, PlastoidsClaims.UseAccess),
        };
        claims.AddRange(subject.Roles.Select(r => new Claim(PlastoidsClaims.Role, r)));
        claims.AddRange(subject.OrganisationIds.Select(o => new Claim(PlastoidsClaims.OrganisationId, o.ToString("D"))));
        claims.AddRange(subject.Amr.Select(a => new Claim(PlastoidsClaims.Amr, a)));
        return Issue(claims, TimeSpan.FromMinutes(_options.AccessTokenMinutes));
    }

    public IssuedToken IssueMfaChallenge(Guid userId, Guid tenantId)
        => Issue(
            [
                new Claim(PlastoidsClaims.Subject, userId.ToString("D")),
                new Claim(PlastoidsClaims.TenantId, tenantId.ToString("D")),
                new Claim(PlastoidsClaims.TokenUse, PlastoidsClaims.UseMfaChallenge),
            ],
            TimeSpan.FromMinutes(_options.MfaChallengeMinutes));

    public Guid? ValidateMfaChallenge(string token)
    {
        var result = _handler.ValidateTokenAsync(token, ValidationParameters(_options, _key)).GetAwaiter().GetResult();
        if (!result.IsValid)
        {
            return null;
        }

        var use = result.ClaimsIdentity.FindFirst(PlastoidsClaims.TokenUse)?.Value;
        var sub = result.ClaimsIdentity.FindFirst(PlastoidsClaims.Subject)?.Value;
        return use == PlastoidsClaims.UseMfaChallenge && Guid.TryParse(sub, out var id) ? id : null;
    }

    private IssuedToken Issue(IEnumerable<Claim> claims, TimeSpan lifetime)
    {
        var now = clock.UtcNow;
        var expires = now.Add(lifetime);
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256),
        };
        return new IssuedToken(_handler.CreateToken(descriptor), expires);
    }

    internal static TokenValidationParameters ValidationParameters(AuthOptions options, SecurityKey key) => new()
    {
        ValidIssuer = options.Issuer,
        ValidAudience = options.Audience,
        IssuerSigningKey = key,
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = PlastoidsClaims.Subject,
        RoleClaimType = PlastoidsClaims.Role,
    };
}

public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>JWT bearer authentication + the role/MFA policies from build prompt §2.10.</summary>
    public static IServiceCollection AddPlastoidsAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));
        services.AddSingleton<ITokenService, JwtTokenService>();

        var auth = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        var signingKey = configuration[$"Secrets:{JwtTokenService.SigningKeyName}"]
            ?? throw new InvalidOperationException($"Secrets:{JwtTokenService.SigningKeyName} is not configured.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = JwtTokenService.ValidationParameters(auth, new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)));
                o.Events = new JwtBearerEvents
                {
                    // Only access tokens may call the API; MFA-challenge tokens are consumed by one endpoint explicitly.
                    OnTokenValidated = ctx =>
                    {
                        if (ctx.Principal?.FindFirst(PlastoidsClaims.TokenUse)?.Value != PlastoidsClaims.UseAccess)
                        {
                            ctx.Fail("not_an_access_token");
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Authenticated, p => p.RequireAuthenticatedUser())
            .AddPolicy(Policies.Learner, p => p.RequireRole(RoleNames.Learner))
            .AddPolicy(Policies.Tutor, p => p.RequireRole(RoleNames.Tutor).RequireClaim(PlastoidsClaims.Amr, PlastoidsClaims.AmrMfa))
            .AddPolicy(Policies.Sponsor, p => p.RequireRole(RoleNames.Sponsor).RequireClaim(PlastoidsClaims.Amr, PlastoidsClaims.AmrMfa))
            .AddPolicy(Policies.Staff, p => p.RequireRole(RoleNames.Staff).RequireClaim(PlastoidsClaims.Amr, PlastoidsClaims.AmrMfa))
            .AddPolicy(Policies.Mfa, p => p.RequireAuthenticatedUser().RequireClaim(PlastoidsClaims.Amr, PlastoidsClaims.AmrMfa))
            .AddPolicy(Policies.StaffAdmin, p => p.RequireRole(RoleNames.Staff).RequireClaim(PlastoidsClaims.Amr, PlastoidsClaims.AmrMfa));

        return services;
    }
}
