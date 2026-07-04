using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Platform.Connectors;

/// <summary>
/// Defines a pluggable connector that self-registers its services.
/// Implementations are discovered by <see cref="ConnectorRegistry"/> at startup.
/// </summary>
public interface IConnectorModule
{
    /// <summary>Unique name identifying this connector (used for logging and diagnostics).</summary>
    string Name { get; }

    /// <summary>Registers the connector's services into the DI container.</summary>
    void Register(IServiceCollection services, IConfiguration configuration);
}
