namespace ApiPlatform.Integration.Eventing;

/// <summary>
/// Canonical integration event publisher. Connectors and hosts publish domain events
/// through this seam; the concrete transport (in-memory, Service Bus, Event Grid) is
/// selected from configuration and defaults to in-memory so the repo runs offline.
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync(string eventType, object data, CancellationToken ct = default);
}
