using ApiPlatform.EventSource.Models;

namespace ApiPlatform.EventSource;

/// <summary>
/// Pluggable emission target for domain events.
/// </summary>
public interface IEventSink
{
    ValueTask EmitAsync(WorkItemChanged change, CancellationToken ct = default);
}

/// <summary>
/// Default stub sink — collects emitted events in memory.
/// Suitable for offline use and unit tests.
/// </summary>
public sealed class InMemoryEventSink : IEventSink
{
    private readonly List<WorkItemChanged> _emitted = [];

    public IReadOnlyList<WorkItemChanged> Emitted => _emitted;

    public ValueTask EmitAsync(WorkItemChanged change, CancellationToken ct = default)
    {
        _emitted.Add(change);
        return ValueTask.CompletedTask;
    }
}
