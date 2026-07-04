using ApiPlatform.Platform.Connectors;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Integration.Eventing;

/// <summary>
/// Self-registering eventing connector. Selects the publisher transport from
/// <c>Eventing:Mode</c> (InMemory | ServiceBus | EventGrid) and falls back to the
/// in-memory publisher whenever the chosen transport is missing its connection config —
/// so the platform clones-and-runs offline with zero core wiring.
/// </summary>
public sealed class EventingConnectorModule : IConnectorModule
{
    public string Name => "Eventing";

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        var mode = configuration["Eventing:Mode"] ?? "InMemory";

        switch (mode.ToLowerInvariant())
        {
            case "servicebus":
                var sbConn = configuration["Eventing:ServiceBus:ConnectionString"];
                var sbEntity = configuration["Eventing:ServiceBus:Entity"] ?? "platform-events";
                if (!string.IsNullOrWhiteSpace(sbConn))
                {
                    services.AddSingleton<IIntegrationEventPublisher>(
                        _ => new ServiceBusEventPublisher(sbConn, sbEntity));
                    return;
                }
                break;

            case "eventgrid":
                var egEndpoint = configuration["Eventing:EventGrid:Endpoint"];
                var egKey = configuration["Eventing:EventGrid:AccessKey"];
                if (!string.IsNullOrWhiteSpace(egEndpoint) && !string.IsNullOrWhiteSpace(egKey))
                {
                    services.AddSingleton<IIntegrationEventPublisher>(
                        _ => new EventGridEventPublisher(egEndpoint, egKey));
                    return;
                }
                break;
        }

        // Default / offline fallback — in-memory, also resolvable as its concrete type.
        services.AddSingleton<InMemoryEventPublisher>();
        services.AddSingleton<IIntegrationEventPublisher>(
            sp => sp.GetRequiredService<InMemoryEventPublisher>());
    }
}
