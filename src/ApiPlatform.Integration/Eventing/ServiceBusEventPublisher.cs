using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace ApiPlatform.Integration.Eventing;

/// <summary>
/// Azure Service Bus publisher (kept — Azure architecture is retained). Wired only when
/// <c>Eventing:Mode=ServiceBus</c> and a connection string is configured; otherwise the
/// in-memory publisher is the default.
/// </summary>
public sealed class ServiceBusEventPublisher : IIntegrationEventPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly string _entity;

    public ServiceBusEventPublisher(string connectionString, string entity)
    {
        _client = new ServiceBusClient(connectionString);
        _entity = entity;
    }

    public async Task PublishAsync(string eventType, object data, CancellationToken ct = default)
    {
        await using var sender = _client.CreateSender(_entity);
        var message = new ServiceBusMessage(JsonSerializer.SerializeToUtf8Bytes(data))
        {
            Subject = eventType,
            ContentType = "application/json",
        };
        await sender.SendMessageAsync(message, ct);
    }

    public ValueTask DisposeAsync() => _client.DisposeAsync();
}
