using System.Text.Json;

namespace ApiPlatform.EventSource;

/// <summary>
/// Tracks the last-committed position in the work-item change stream so the emitter
/// can skip already-processed events on restart (at-least-once delivery).
/// The position is the <see cref="Models.WorkItemChanged.At"/> timestamp of the
/// last fully-emitted group.
/// </summary>
public interface IEventSourcePositionStore
{
    /// <summary>Returns the timestamp of the last fully-committed event group.</summary>
    DateTimeOffset GetPosition();

    /// <summary>Advances the stored position to <paramref name="to"/> if it is later.</summary>
    void Advance(DateTimeOffset to);
}

/// <summary>
/// In-memory position store. Default for local development and tests.
/// Position is lost when the process exits; the emitter replays from the beginning
/// of the feed on restart.
/// </summary>
public sealed class InMemoryEventSourcePositionStore : IEventSourcePositionStore
{
    private DateTimeOffset _position = DateTimeOffset.MinValue;

    /// <inheritdoc />
    public DateTimeOffset GetPosition() => _position;

    /// <inheritdoc />
    public void Advance(DateTimeOffset to)
    {
        if (to > _position)
            _position = to;
    }
}

/// <summary>
/// File-backed position store. Persists the committed position to a JSON file so
/// the emitter can resume after a process restart without replaying all events.
/// A new instance over the same directory reads the previously persisted position.
/// </summary>
public sealed class DurableFileEventSourcePositionStore : IEventSourcePositionStore
{
    private readonly string _filePath;
    private readonly object _lock = new();

    public DurableFileEventSourcePositionStore(string directory)
    {
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "eventsource-position.json");
    }

    /// <inheritdoc />
    public DateTimeOffset GetPosition()
    {
        lock (_lock)
            return ReadFromFile();
    }

    /// <inheritdoc />
    public void Advance(DateTimeOffset to)
    {
        lock (_lock)
        {
            if (to <= ReadFromFile())
                return;

            var state = new EventSourcePositionState(to);
            File.WriteAllText(_filePath, JsonSerializer.Serialize(state));
        }
    }

    private DateTimeOffset ReadFromFile()
    {
        if (!File.Exists(_filePath))
            return DateTimeOffset.MinValue;

        try
        {
            var text  = File.ReadAllText(_filePath);
            var state = JsonSerializer.Deserialize<EventSourcePositionState>(text);
            return state?.Position ?? DateTimeOffset.MinValue;
        }
        catch
        {
            return DateTimeOffset.MinValue;
        }
    }
}

/// <summary>Serializable state written by <see cref="DurableFileEventSourcePositionStore"/>.</summary>
internal sealed record EventSourcePositionState(DateTimeOffset Position);
