using ApiPlatform.Integration.Acl;
using ApiPlatform.Platform.Connectors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Integration.Acl.Databricks;

/// <summary>
/// Self-registering connector for Databricks analytics.
/// Discovered automatically by <see cref="ConnectorRegistry.AddConnectors"/> at startup
/// via <c>AddIntegration()</c> — no manual registration required.
///
/// Default: <see cref="StubDatabricksClient"/> — fixture data; no Databricks subscription needed.
/// Live:    set <c>Databricks:Mode=Live</c> AND <c>Databricks:ConnectionString</c> to activate
///          <see cref="DatabricksSqlClient"/>. Both keys must be present; missing either keeps
///          the stub in place.
/// </summary>
public sealed class DatabricksConnectorModule : IConnectorModule
{
    public string Name => "Databricks";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        var mode             = configuration["Databricks:Mode"] ?? "Stub";
        var connectionString = configuration["Databricks:ConnectionString"];

        if (mode.Equals("Live", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(connectionString))
        {
            // Live path — only reachable when both keys are explicitly configured.
            services.AddSingleton<IDatabricksClient>(
                _ => new DatabricksSqlClient(connectionString));
        }
        else
        {
            // Stub default — zero configuration required; safe for local development and CI.
            services.AddSingleton<IDatabricksClient, StubDatabricksClient>();
        }

        services.AddSingleton<IInsightSource, DatabricksInsightSource>();
    }
}
