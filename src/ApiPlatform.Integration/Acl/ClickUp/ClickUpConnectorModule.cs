using ApiPlatform.Integration.Acl;
using ApiPlatform.Platform.Connectors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Integration.Acl.ClickUp;

/// <summary>
/// Self-registering connector for the ClickUp work-item source.
/// Discovered automatically by <see cref="ConnectorRegistry.AddConnectors"/> at startup —
/// no changes to Program.cs or any other core file are required.
/// </summary>
public sealed class ClickUpConnectorModule : IConnectorModule
{
    public string Name => "ClickUp";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        // Stub client — replace with a real typed HttpClient when ClickUp:Mode = "Live"
        services.AddSingleton<StubClickUpClient>();
        services.AddSingleton<IWorkItemSource, ClickUpWorkItemSource>();
    }
}
