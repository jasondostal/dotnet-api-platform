using System.Collections.Concurrent;

namespace ApiPlatform.Platform.AspNetCore.Idempotency;

/// <summary>
/// Thread-safe in-memory idempotency store with atomic set-if-absent semantics.
/// Suitable for single-instance deployments and integration testing.
///
/// Concurrent requests that share the same key are handled as follows:
/// <list type="bullet">
///   <item>The first caller to <see cref="TryBeginAsync"/> wins and executes the handler.</item>
///   <item>
///     Any concurrent loser that calls <see cref="GetAsync"/> before the winner completes
///     will block until the winner calls <see cref="SetAsync"/> (or <see cref="AbandonAsync"/>),
///     guaranteeing exactly one handler execution per key.
///   </item>
/// </list>
/// Replace with a distributed store (Redis, Azure Table, etc.) for multi-instance deployments.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    // Completed entries: written once, read many.
    private readonly ConcurrentDictionary<string, IdempotencyEntry> _done = new();

    // In-flight slots: added by TryBeginAsync winner, completed (or cancelled) by SetAsync/AbandonAsync.
    // The TaskCompletionSource<IdempotencyEntry?> allows losers to await the winner's result.
    private readonly ConcurrentDictionary<string, TaskCompletionSource<IdempotencyEntry?>> _inflight = new();

    /// <inheritdoc/>
    /// <remarks>
    /// If an entry is completed it returns immediately.
    /// If an entry is in-flight (winner is still executing) this call blocks until the
    /// winner calls <see cref="SetAsync"/> or <see cref="AbandonAsync"/>.
    /// </remarks>
    public async ValueTask<IdempotencyEntry?> GetAsync(string key, CancellationToken ct = default)
    {
        // Fast path: already completed.
        if (_done.TryGetValue(key, out var entry))
            return entry;

        // Slow path: winner is still executing — await its completion signal.
        if (_inflight.TryGetValue(key, out var tcs))
        {
            try
            {
                return await tcs.Task.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Atomically creates an in-flight slot via <see cref="ConcurrentDictionary{TKey,TValue}.TryAdd"/>.
    /// This is the race-free "begin" primitive: no Get-then-Set window exists because the slot
    /// is inserted before the handler executes.
    /// </remarks>
    public ValueTask<bool> TryBeginAsync(string key, CancellationToken ct = default)
    {
        // Already completed — slot is owned.
        if (_done.ContainsKey(key))
            return ValueTask.FromResult(false);

        var tcs = new TaskCompletionSource<IdempotencyEntry?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // Atomic: only one caller succeeds in adding the slot.
        bool won = _inflight.TryAdd(key, tcs);
        return ValueTask.FromResult(won);
    }

    /// <inheritdoc/>
    public ValueTask SetAsync(string key, IdempotencyEntry entry, CancellationToken ct = default)
    {
        // Move entry to the completed set; first writer wins.
        _done.TryAdd(key, entry);

        // Signal any waiting losers with the completed entry.
        if (_inflight.TryRemove(key, out var tcs))
            tcs.TrySetResult(entry);

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Signals waiting losers with a <c>null</c> result so they are unblocked immediately
    /// rather than waiting indefinitely. The middleware interprets a null result as
    /// "winner failed; fall through."
    /// </remarks>
    public ValueTask AbandonAsync(string key, CancellationToken ct = default)
    {
        if (_inflight.TryRemove(key, out var tcs))
            tcs.TrySetResult(null);

        return ValueTask.CompletedTask;
    }
}
