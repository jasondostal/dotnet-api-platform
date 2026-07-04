using ApiPlatform.Integration.Acl;
using ApiPlatform.Platform.Connectors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Integration.Acl.Cards;

/// <summary>
/// Self-registering connector for the Cards Platform source system.
/// Discovered automatically by <see cref="ConnectorRegistry.AddConnectors"/> at startup.
/// </summary>
public sealed class CardsConnectorModule : IConnectorModule
{
    public string Name => "Cards";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<StubCardsPlatformClient>();
        services.AddSingleton<IAccountVendor, CardsPlatformAccountSource>();
    }
}
