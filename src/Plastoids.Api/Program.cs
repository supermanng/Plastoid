using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Plastoids.Api;
using Plastoids.Infrastructure;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Observability;
using Plastoids.Infrastructure.Security;
using Plastoids.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Plastoids.Modules.Applications;
using Plastoids.Modules.Audit;
using Plastoids.Modules.BackOffice;
using Plastoids.Modules.Booking;
using Plastoids.Modules.Catalogue;
using Plastoids.Modules.Certificates;
using Plastoids.Modules.Lms;
using Plastoids.Modules.Identity;
using Plastoids.Modules.Ledger;
using Plastoids.Modules.Payments;
using Plastoids.Modules.Notifications;
using Plastoids.Modules.Payouts;
using Plastoids.Modules.Sponsorship;
using Plastoids.Modules.Tutors;
using Plastoids.Modules.Wallet;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.AddPlastoidsLogging();

var configuration = builder.Configuration;
var services = builder.Services;

services.AddPlastoidsInfrastructure(configuration);
services.AddPlastoidsPipeline();
services.AddPlastoidsAuthentication(configuration);
services.AddPlastoidsTelemetry(configuration["Otlp:Endpoint"]);

// ---- Modules (order = migration order) ----
services.AddModule<IdentityModule>(configuration);
services.AddModule<AuditModule>(configuration);
services.AddModule<TutorsModule>(configuration);
services.AddModule<CatalogueModule>(configuration);
services.AddModule<LedgerModule>(configuration);
services.AddModule<PaymentsModule>(configuration);
services.AddModule<WalletModule>(configuration);
services.AddModule<BookingModule>(configuration);
services.AddModule<LmsModule>(configuration);
services.AddModule<CertificatesModule>(configuration);
services.AddModule<SponsorshipModule>(configuration);
services.AddModule<ApplicationsModule>(configuration);
services.AddModule<PayoutsModule>(configuration);
services.AddModule<NotificationsModule>(configuration);
services.AddModule<BackOfficeModule>(configuration);

// Enums travel as camelCase strings on the wire ("monday", "selfPaced"), per blueprint §17 JSON conventions.
services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase)));

// CORS is opt-in: the web app is served from the same origin (Vite proxy locally, nginx in Docker). Set Cors:AllowedOrigins
// (e.g. ["https://app.plastoids.ng"]) only when the frontend is hosted on another origin.
var corsOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
if (corsOrigins.Length > 0)
{
    services.AddCors(o => o.AddDefaultPolicy(p => p
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders(CorrelationIdMiddleware.HeaderName, "Content-Disposition")));
}

services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
{
    // The exception handler clears the response, so the id is read back from the request (the middleware copies it there).
    var correlationId = ctx.HttpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
    ctx.HttpContext.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
    ctx.ProblemDetails.Extensions["correlationId"] = correlationId;
});
services.AddExceptionHandler<GlobalExceptionHandler>();
services.AddOpenApi("v1");
// Per-IP limits (blueprint §17: stricter on /auth/*). Tests raise them through RateLimiting:* settings.
var authPermitsPerMinute = configuration.GetValue("RateLimiting:AuthPermitsPerMinute", 10);
var globalTokensPerMinute = configuration.GetValue("RateLimiting:GlobalTokensPerMinute", 300);
services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermitsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx => RateLimitPartition.GetTokenBucketLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
        _ => new TokenBucketRateLimiterOptions { TokenLimit = globalTokensPerMinute, TokensPerPeriod = globalTokensPerMinute, ReplenishmentPeriod = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

services.AddPlastoidsHealthChecks(configuration);

var app = builder.Build();

// ---- CLI commands ----
if (args.Length > 0 && args[0] == "migrate")
{
    await Migrator.MigrateAllAsync(app.Services);
    Log.Information("Migrations applied.");
    return;
}

// Behind a load balancer the rate limiters and the admin IP allow-list must see the client, not the proxy (Hosting:KnownProxies / KnownNetworks).
var forwarded = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, ForwardLimit = 2 };
forwarded.KnownNetworks.Clear();
forwarded.KnownProxies.Clear();
foreach (var proxy in configuration.GetSection("Hosting:KnownProxies").Get<string[]>() ?? [])
{
    if (System.Net.IPAddress.TryParse(proxy, out var address))
    {
        forwarded.KnownProxies.Add(address);
    }
}

foreach (var network in configuration.GetSection("Hosting:KnownNetworks").Get<string[]>() ?? [])
{
    var parts = network.Split('/');
    if (parts.Length == 2 && System.Net.IPAddress.TryParse(parts[0], out var prefix) && int.TryParse(parts[1], out var length))
    {
        forwarded.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, length));
    }
}

if (forwarded.KnownProxies.Count > 0 || forwarded.KnownNetworks.Count > 0)
{
    // With both lists empty ASP.NET Core would trust every proxy, so the middleware is only added when proxies are named.
    app.UseForwardedHeaders(forwarded);
}

// Production never runs on fakes: every integration must name a real adapter (secrets are validated by the adapters themselves).
if (app.Environment.IsProduction())
{
    string[] integrationKeys = ["Integrations:PaymentGateway", "Integrations:PayoutProvider", "Integrations:Kyc", "Integrations:BankResolver", "Integrations:Video", "Integrations:LiveVideo", "Integrations:Calendar", "Integrations:Email", "Sms:Provider"];
    var fakes = integrationKeys
        .Where(key => string.IsNullOrWhiteSpace(configuration[key]) || string.Equals(configuration[key], "fake", StringComparison.OrdinalIgnoreCase))
        .ToList();
    if (fakes.Count > 0)
    {
        throw new InvalidOperationException("Production cannot start with fake integrations: " + string.Join(", ", fakes));
    }
}

app.UseMiddleware<CorrelationIdMiddleware>();
if (corsOrigins.Length > 0)
{
    app.UseCors();
}
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AdminIpAllowListMiddleware>();

app.MapOpenApi();
if (app.Environment.IsDevelopment())
{
    // Swagger UI over the built-in OpenAPI document: http://localhost:5080/swagger
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "Plastoids API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "Plastoids API";
    });
}
app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new()
{
    Predicate = r => r.Tags.Contains(PlastoidsHealthChecks.ReadyTag),
    ResponseWriter = PlastoidsHealthChecks.WriteJsonAsync,
});

var v1 = app.MapGroup("/api/v1");
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Testing"))
{
    // Proves the problem-details path for unhandled exceptions (Sprint 9 security pass); never mapped in staging or production.
    v1.MapGet("/diagnostics/throw", DiagnosticsThrow)
        .WithName("DiagnosticsThrow").ExcludeFromDescription().AllowAnonymous();
}
foreach (var module in app.Services.GetServices<Plastoids.Infrastructure.Modules.IModule>())
{
    module.MapEndpoints(v1);
}

await app.RunAsync();

static IResult DiagnosticsThrow() => throw new InvalidOperationException("Diagnostics: deliberate failure");

namespace Plastoids.Api
{
    /// <summary>Exposed so WebApplicationFactory can host the app in integration tests.</summary>
    public partial class Program
    {
    }

    internal sealed class CorrelationIdMiddleware(RequestDelegate next)
    {
        public const string HeaderName = "X-Correlation-Id";

        public async Task InvokeAsync(HttpContext context)
        {
            var id = context.Request.Headers[HeaderName].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(id))
            {
                id = Guid.CreateVersion7().ToString("N");
                context.Request.Headers[HeaderName] = id;
            }

            context.Response.Headers[HeaderName] = id;
            using (Serilog.Context.LogContext.PushProperty("correlationId", id))
            {
                await next(context);
            }
        }
    }

    /// <summary>Unhandled exceptions become RFC 9457 problems without stack traces. Cross-tenant writes are 403.</summary>
    internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var (status, code, detail) = exception switch
            {
                Plastoids.Infrastructure.Persistence.Interceptors.CrossTenantWriteException => (StatusCodes.Status403Forbidden, "cross_tenant_write", "The operation targets another tenant."),
                Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "concurrency_conflict", "The record was modified by someone else. Reload and retry."),
                _ => (StatusCodes.Status500InternalServerError, "internal_error", "An unexpected error occurred."),
            };

            logger.LogError(exception, "Unhandled exception → {Status} {Code}", status, code);
            httpContext.Response.StatusCode = status;
            httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName] = httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
            await httpContext.Response.WriteAsJsonAsync(new
            {
                type = $"https://docs.plastoids.ng/errors/{code}",
                title = code,
                status,
                detail,
                code,
                correlationId = httpContext.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString(),
            }, cancellationToken);
            return true;
        }
    }
}
