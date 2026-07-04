using ApiPlatform.Integration.Acl;
using ApiPlatform.Platform.Connectors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Integration.Acl.Plaid;

/// <summary>
/// Self-registering connector for Plaid.
/// Discovered automatically by <see cref="ConnectorRegistry.AddConnectors"/> at startup.
/// <para>
/// The connector registers both its stub client and the <see cref="PlaidAccountSource"/>
/// vendor unconditionally — the enabled/disabled gate lives inside the source class,
/// which returns an empty result when <c>Plaid:Enabled</c> is false (the default).
/// This means the existing account set (CoreBanking + Cards) is unaffected until
/// the operator explicitly opts in.
/// </para>
/// </summary>
public sealed class PlaidConnectorModule : IConnectorModule
{
    public string Name => "Plaid";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        var enabled = bool.TryParse(configuration["Plaid:Enabled"], out var on) && on;
        services.AddSingleton<StubPlaidClient>();
        // Additive registration — does NOT replace CoreBanking or Cards vendors.
        services.AddSingleton<IAccountVendor>(sp =>
            new PlaidAccountSource(sp.GetRequiredService<StubPlaidClient>(), enabled));
    }
}
