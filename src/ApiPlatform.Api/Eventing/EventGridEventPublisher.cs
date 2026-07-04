using Azure;
using Azure.Messaging;
using Azure.Messaging.EventGrid;

namespace ApiPlatform.Api.Eventing;

/// <summary>
/// Publishes CloudEvents to an Azure Event Grid topic.
/// Selected when <c>EVENT_PUBLISHER_TYPE=EventGrid</c> with both
/// <c>EVENTGRID_TOPIC_ENDPOINT</c> and <c>EVENTGRID_TOPIC_KEY</c> configured.
/// Publish failures are logged and rethrown so the caller can decide whether to
/// treat the operation as failed — they are NOT silently swallowed.
/// </summary>
public sealed class EventGridEventPublisher : IEventPublisher
{
    private readonly EventGridPublisherClient     _client;
    private readonly ILogger<EventGridEventPublisher> _logger;

    public EventGridEventPublisher(IConfiguration config, ILogger<EventGridEventPublisher> logger)
    {
        _logger = logger;

        var endpoint = config["EVENTGRID_TOPIC_ENDPOINT"]
            ?? throw new InvalidOperationException(
                "EVENTGRID_TOPIC_ENDPOINT is required when EVENT_PUBLISHER_TYPE=EventGrid.");
        var key = config["EVENTGRID_TOPIC_KEY"]
            ?? throw new InvalidOperationException(
                "EVENTGRID_TOPIC_KEY is required when EVENT_PUBLISHER_TYPE=EventGrid.");

        _client = new EventGridPublisherClient(new Uri(endpoint), new AzureKeyCredential(key));
    }

    public async Task PublishAccountTouchedAsync(Guid accountId, CancellationToken ct = default)
    {
        var cloudEvent = new CloudEvent(
            source:               "/northwind/api",
            type:                 "northwind.account.touched",
            jsonSerializableData: (object?)null)
        {
            Subject = accountId.ToString(),
        };

        try
        {
            await _client.SendEventAsync(cloudEvent, ct);
            _logger.LogInformation(
                "Published northwind.account.touched for account {AccountId}", accountId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish northwind.account.touched for account {AccountId}.", accountId);
            throw;
        }
    }
}
