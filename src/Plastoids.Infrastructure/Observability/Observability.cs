using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

namespace Plastoids.Infrastructure.Observability;

public static class Observability
{
    public const string ServiceName = "plastoids-api";

    public static WebApplicationBuilder AddPlastoidsLogging(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("service", ServiceName)
            // PII never reaches logs: sensitive members are masked by PiiDestructuringPolicy, graphs stop at depth 2.
            .Destructure.With<PiiDestructuringPolicy>()
            .Destructure.ToMaximumDepth(2)
            .WriteTo.Console(new RenderedCompactJsonFormatter()));
        return builder;
    }

    public static IServiceCollection AddPlastoidsTelemetry(this IServiceCollection services, string? otlpEndpoint)
    {
        var otel = services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(ServiceName))
            .WithTracing(t =>
            {
                t.AddAspNetCoreInstrumentation(o => o.Filter = ctx => !ctx.Request.Path.StartsWithSegments("/health"));
                t.AddHttpClientInstrumentation();
                t.AddSource("Plastoids.*");
            })
            .WithMetrics(m =>
            {
                m.AddAspNetCoreInstrumentation();
                m.AddHttpClientInstrumentation();
                m.AddMeter("Plastoids.*");
            });
        // OTLP exporter: OpenTelemetry.Exporter.OpenTelemetryProtocol 1.14+ requires .NET 10 and 1.13 has known CVEs,
        // so no exporter is wired until the runtime moves to .NET 10 (ADR 0005). The endpoint setting is kept for that day.
        _ = otel;
        _ = otlpEndpoint;
        return services;
    }
}
