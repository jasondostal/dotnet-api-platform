using ApiPlatform.Integration.Acl;
using ApiPlatform.Platform.Connectors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Integration.Acl.CoreBanking;

/// <summary>
/// Self-registering connector for the Core Banking source system.
/// Discovered automatically by <see cref="ConnectorRegistry.AddConnectors"/> at startup.
/// </summary>
public sealed class CoreBankingConnectorModule : IConnectorModule
{
    public string Name => "CoreBanking";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<StubCoreBankingClient>();
        services.AddSingleton<IAccountVendor, CoreBankingAccountSource>();
        services.AddSingleton<ICustomerSource, CoreBankingCustomerSource>();
        // Write capability (create/change). No audit code here — the governance proxy audits it.
        services.AddSingleton<IAccountWriter, CoreBankingAccountWriter>();
    }
}
