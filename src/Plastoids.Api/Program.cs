using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Diagnostics;
using Plastoids.Api;
using Plastoids.Infrastructure;
using Plastoids.Infrastructure.Http;
using Plastoids.Infrastructure.Observability;
using Plastoids.Infrastructure.Security;
using Plastoids.Infrastructure.Persistence;
using Plastoids.Modules.Audit;
using Plastoids.Modules.Catalogue;
using Plastoids.Modules.Identity;
using Plastoids.Modules.Tutors;
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

// Enums travel as camelCase strings on the wire ("monday", "selfPaced"), per blueprint §17 JSON conventions.
services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase)));

services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
{
    ctx.ProblemDetails.Extensions["correlationId"] = ctx.HttpContext.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString();
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

app.UseMiddleware<CorrelationIdMiddleware>();
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
foreach (var module in app.Services.GetServices<Plastoids.Infrastructure.Modules.IModule>())
{
    module.MapEndpoints(v1);
}

await app.RunAsync();

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
            await httpContext.Response.WriteAsJsonAsync(new
            {
                type = $"https://docs.plastoids.ng/errors/{code}",
                title = code,
                status,
                detail,
                code,
                correlationId = httpContext.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString(),
            }, cancellationToken);
            return true;
        }
    }
}
