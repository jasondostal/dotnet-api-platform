using ApiPlatform.Platform.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ApiPlatform.Platform.Telemetry;

/// <summary>
/// OpenTelemetry wiring for non-web platform hosts (Poller, EventSource, MCP).
/// Registers the platform <see cref="PlatformDiagnostics.ActivitySource"/> and a service
/// resource so traces/metrics carry consistent identity. No exporter is wired by default —
/// the repo clones-and-runs offline. Add an exporter (OTLP/Console/Azure Monitor) via the
/// returned builder or the standard OTEL_* environment variables in a real deployment.
/// </summary>
public static class TelemetrySetup
{
    /// <summary>
    /// Adds platform OpenTelemetry tracing + metrics for an off-web host identified by
    /// <paramref name="serviceName"/>.
    /// </summary>
    public static IServiceCollection AddPlatformTelemetry(
        this IServiceCollection services,
        string serviceName)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing.AddSource(PlatformDiagnostics.ActivitySourceName))
            .WithMetrics(metrics => metrics.AddMeter(PlatformDiagnostics.ActivitySourceName));

        return services;
    }
}
