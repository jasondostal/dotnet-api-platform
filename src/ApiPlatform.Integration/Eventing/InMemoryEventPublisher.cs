using System.Collections.Concurrent;

namespace ApiPlatform.Integration.Eventing;

/// <summary>A published event captured by <see cref="InMemoryEventPublisher"/>.</summary>
public sealed record PublishedEvent(string EventType, object Data);

/// <summary>
/// The default, offline-safe publisher: captures events in memory so eventing tests and
/// local runs work without any Azure connection string.
/// </summary>
public sealed class InMemoryEventPublisher : IIntegrationEventPublisher
{
    private readonly ConcurrentQueue<PublishedEvent> _published = new();

    public IReadOnlyCollection<PublishedEvent> Published => _published.ToArray();

    public Task PublishAsync(string eventType, object data, CancellationToken ct = default)
    {
        _published.Enqueue(new PublishedEvent(eventType, data));
        return Task.CompletedTask;
    }
}
