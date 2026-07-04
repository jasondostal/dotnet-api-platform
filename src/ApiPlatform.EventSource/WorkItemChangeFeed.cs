using ApiPlatform.EventSource.Models;
using System.Runtime.CompilerServices;

namespace ApiPlatform.EventSource;

/// <summary>
/// Produces a stream of <see cref="WorkItemChanged"/> events.
/// </summary>
public interface IWorkItemChangeFeed
{
    IAsyncEnumerable<WorkItemChanged> StreamAsync(CancellationToken ct);
}

/// <summary>
/// Stub feed that replays a fixed in-memory sequence of changes.
/// Inject a custom <see cref="IEnumerable{WorkItemChanged}"/> for testing.
/// </summary>
public sealed class InMemoryWorkItemChangeFeed : IWorkItemChangeFeed
{
    private readonly IReadOnlyList<WorkItemChanged> _changes;

    /// <summary>Creates a feed with a built-in demo sequence (created/updated/closed).</summary>
    public InMemoryWorkItemChangeFeed() : this(TimeProvider.System) { }

    /// <summary>Creates a feed with a built-in demo sequence driven by the supplied time provider.</summary>
    public InMemoryWorkItemChangeFeed(TimeProvider timeProvider)
        : this(BuildDefaultChanges(timeProvider)) { }

    /// <summary>Creates a feed that replays <paramref name="changes"/> in order.</summary>
    public InMemoryWorkItemChangeFeed(IEnumerable<WorkItemChanged> changes)
        => _changes = changes.ToList();

    private static IEnumerable<WorkItemChanged> BuildDefaultChanges(TimeProvider timeProvider)
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var now = timeProvider.GetUtcNow();
        return
        [
            new() { WorkItemId = id1, ChangeType = "Created", At = now },
            new() { WorkItemId = id2, ChangeType = "Created", At = now.AddSeconds(1) },
            new() { WorkItemId = id1, ChangeType = "Updated", At = now.AddSeconds(2) },
            new() { WorkItemId = id2, ChangeType = "Closed",  At = now.AddSeconds(3) },
        ];
    }

    public async IAsyncEnumerable<WorkItemChanged> StreamAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var change in _changes)
        {
            ct.ThrowIfCancellationRequested();
            yield return change;
            await Task.Yield();
        }
    }
}
