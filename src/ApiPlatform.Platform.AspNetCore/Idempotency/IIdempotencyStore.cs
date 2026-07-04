namespace ApiPlatform.Platform.AspNetCore.Idempotency;

/// <summary>
/// Stores and retrieves captured HTTP responses keyed by a composite
/// (method, path, principal, Idempotency-Key) string.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Returns the stored entry for <paramref name="key"/>, or <c>null</c> if none exists.
    /// Implementations may block until an in-flight entry for the same key is complete.
    /// </summary>
    ValueTask<IdempotencyEntry?> GetAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Atomically claims the slot identified by <paramref name="key"/>.
    /// Returns <c>true</c> if this caller inserted the slot (won the race) and must
    /// subsequently call <see cref="SetAsync"/> to complete it, or
    /// <see cref="AbandonAsync"/> if the handler fails.
    /// Returns <c>false</c> if a slot already exists (completed or in-flight).
    /// </summary>
    ValueTask<bool> TryBeginAsync(string key, CancellationToken ct = default);

    /// <summary>Stores the completed entry and unblocks any waiters on this key.</summary>
    ValueTask SetAsync(string key, IdempotencyEntry entry, CancellationToken ct = default);

    /// <summary>
    /// Releases the slot for <paramref name="key"/> without storing an entry,
    /// unblocking any waiters so they do not hang indefinitely.
    /// Default no-op; override in implementations that track in-flight state.
    /// </summary>
    ValueTask AbandonAsync(string key, CancellationToken ct = default) => ValueTask.CompletedTask;
}

/// <summary>Captured state of a completed HTTP response.</summary>
public sealed record IdempotencyEntry(int StatusCode, string ContentType, byte[] Body);
