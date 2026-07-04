using System.Collections.Concurrent;

namespace ApiPlatform.Api.Eventing;

/// <summary>
/// Default offline-safe publisher: records published events in memory.
/// Suitable for local development and unit tests; requires no cloud configuration.
/// Also resolvable as its concrete type for test assertions.
/// </summary>
public sealed class InMemoryApiEventPublisher : IEventPublisher
{
    private readonly ConcurrentQueue<Guid> _published = new();

    /// <summary>All account IDs published since this instance was created.</summary>
    public IReadOnlyCollection<Guid> Published => [.. _published];

    public Task PublishAccountTouchedAsync(Guid accountId, CancellationToken ct = default)
    {
        _published.Enqueue(accountId);
        return Task.CompletedTask;
    }
}
