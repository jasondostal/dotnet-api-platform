using ApiPlatform.Integration.Acl;
using ApiPlatform.Integration.Acl.Governance;
using ApiPlatform.Platform.Connectors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Integration.Runtime;

/// <summary>
/// DI registration entry point for the integration layer.
/// Registers the routing aggregator and self-discovers all vendor connector modules
/// in this assembly via <see cref="ConnectorRegistry.AddConnectors"/>.
/// </summary>
public static class IntegrationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the full integration stack:
    /// <list type="bullet">
    ///   <item>IAccountSource → RoutingAccountSource (aggregates all IAccountVendor registrations)</item>
    ///   <item>All IConnectorModule implementations in this assembly (CoreBanking, Cards, …)</item>
    /// </list>
    /// Add new vendor connectors by implementing IConnectorModule in this assembly — no other changes needed.
    /// </summary>
    public static IServiceCollection AddIntegration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Routing aggregator — infrastructure, not a vendor, registered here rather than in a connector module
        services.AddSingleton<IAccountSource, RoutingAccountSource>();

        // Self-register all vendor connector modules in this assembly
        services.AddConnectors(configuration, typeof(IntegrationServiceCollectionExtensions).Assembly);

        // Wrap every canonical source in its governed decorator (audit + trace), by construction.
        // A source with no decorator throws here — none can be wired un-governed.
        services.GovernSources();

        return services;
    }
}
