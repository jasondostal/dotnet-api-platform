using System.Text.Json;

namespace ApiPlatform.Poller;

/// <summary>
/// Tracks the last-processed position in a creation feed so the poller
/// knows which records it has already consumed.
/// </summary>
public interface ICursorStore
{
    /// <summary>Returns the timestamp of the last successfully processed record.</summary>
    DateTimeOffset GetCursor();

    /// <summary>
    /// Advances the stored cursor to <paramref name="to"/> if it is later than
    /// the current value.
    /// </summary>
    void Advance(DateTimeOffset to);
}

/// <summary>
/// In-memory cursor store for single-host, non-durable use (dev / testing).
/// Position is lost when the process exits; default for clone-and-run.
/// </summary>
public sealed class InMemoryCursorStore : ICursorStore
{
    private DateTimeOffset _cursor = DateTimeOffset.MinValue;

    /// <inheritdoc />
    public DateTimeOffset GetCursor() => _cursor;

    /// <inheritdoc />
    public void Advance(DateTimeOffset to)
    {
        if (to > _cursor)
            _cursor = to;
    }
}

/// <summary>
/// The persisted state written by <see cref="DurableFileCursorStore"/>.
/// </summary>
public sealed record CursorState(DateTimeOffset Position, DateTimeOffset LastUpdated);

/// <summary>
/// File-backed cursor store. Persists the cursor position as JSON so it survives
/// a process restart. Reads always hit the file (no in-process cache) so a new
/// instance over the same directory reads the previously written position.
/// All reads and writes are guarded by a lock for single-host thread safety.
/// </summary>
public sealed class DurableFileCursorStore : ICursorStore
{
    private readonly string       _filePath;
    private readonly TimeProvider _timeProvider;
    private readonly object       _lock = new();

    public DurableFileCursorStore(string directory, TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "cursor.json");
    }

    /// <inheritdoc />
    public DateTimeOffset GetCursor()
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

            var state = new CursorState(to, _timeProvider.GetUtcNow());
            var json  = JsonSerializer.Serialize(state, PollerJsonContext.Default.CursorState);
            File.WriteAllText(_filePath, json);
        }
    }

    private DateTimeOffset ReadFromFile()
    {
        if (!File.Exists(_filePath))
            return DateTimeOffset.MinValue;

        try
        {
            var text  = File.ReadAllText(_filePath);
            var state = JsonSerializer.Deserialize(text, PollerJsonContext.Default.CursorState);
            return state?.Position ?? DateTimeOffset.MinValue;
        }
        catch
        {
            return DateTimeOffset.MinValue;
        }
    }
}
