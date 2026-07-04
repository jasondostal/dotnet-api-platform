using System.Collections.Concurrent;

namespace ApiPlatform.Api.Eventing;

/// <summary>
/// Thread-safe in-memory ring buffer for webhook-received CloudEvents.
/// Capped at <see cref="Capacity"/> entries — oldest are silently dropped.
/// </summary>
public sealed class ReceivedEventLog
{
    public const int Capacity = 50;

    private readonly ConcurrentQueue<ReceivedEventEntry> _queue = new();
    private int _count;
    private long _totalReceived;

    public long TotalReceived => Interlocked.Read(ref _totalReceived);

    public void Append(ReceivedEventEntry entry)
    {
        _queue.Enqueue(entry);
        Interlocked.Increment(ref _totalReceived);

        if (Interlocked.Increment(ref _count) > Capacity)
        {
            if (_queue.TryDequeue(out _))
                Interlocked.Decrement(ref _count);
        }
    }

    /// <summary>Returns entries most-recent first.</summary>
    public IReadOnlyList<ReceivedEventEntry> GetAll()
        => _queue.Reverse().ToList();
}

public sealed record ReceivedEventEntry(
    string  Type,
    string? Id,
    DateTimeOffset ReceivedAt);
