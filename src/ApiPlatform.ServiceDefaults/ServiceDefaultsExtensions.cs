using ApiPlatform.Platform.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace ApiPlatform.ServiceDefaults;

/// <summary>
/// Aspire-style service defaults. One call — <c>builder.AddServiceDefaults()</c> — gives any
/// host (web or worker) OpenTelemetry (logs + metrics + traces, including the platform
/// ActivitySource), default health checks, service discovery, and a standard HTTP resilience
/// pipeline on every typed client. This is governance-as-inheritance for observability and
/// resilience: a service inherits the cross-cutting defaults rather than re-wiring them.
/// </summary>
public static class ServiceDefaultsExtensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Retry + circuit-breaker + timeout on every typed client by default.
            http.AddStandardResilienceHandler();
            // Resolve logical service names (e.g. "https://accounts") via service discovery.
            http.AddServiceDiscovery();
        });

        return builder;
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddMeter(PlatformDiagnostics.ActivitySourceName);
            })
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(PlatformDiagnostics.ActivitySourceName)
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        // OTLP is the one knob: set OTEL_EXPORTER_OTLP_ENDPOINT (the Aspire dashboard sets it
        // automatically) and logs/metrics/traces flow there. No endpoint -> no exporter, so the
        // repo clones-and-runs offline.
        var useOtlp = !string.IsNullOrWhiteSpace(
            builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlp)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Liveness: the host is up and not deadlocked.
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>
    /// Maps the Aspire convention endpoints for web hosts: <c>/alive</c> (liveness) and
    /// <c>/health</c> (readiness). Call this only on hosts that don't already map <c>/health</c>.
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/alive", new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live"),
        });

        return app;
    }
}
