using Azure;
using Azure.Messaging.EventGrid;

namespace ApiPlatform.Integration.Eventing;

/// <summary>
/// Azure Event Grid publisher (kept). Wired only when <c>Eventing:Mode=EventGrid</c> and an
/// endpoint + access key are configured; otherwise the in-memory publisher is the default.
/// </summary>
public sealed class EventGridEventPublisher : IIntegrationEventPublisher
{
    private readonly EventGridPublisherClient _client;

    public EventGridEventPublisher(string endpoint, string accessKey)
    {
        _client = new EventGridPublisherClient(new Uri(endpoint), new AzureKeyCredential(accessKey));
    }

    public async Task PublishAsync(string eventType, object data, CancellationToken ct = default)
    {
        var evt = new EventGridEvent(
            subject: eventType,
            eventType: eventType,
            dataVersion: "1.0",
            data: BinaryData.FromObjectAsJson(data));

        await _client.SendEventAsync(evt, ct);
    }
}
