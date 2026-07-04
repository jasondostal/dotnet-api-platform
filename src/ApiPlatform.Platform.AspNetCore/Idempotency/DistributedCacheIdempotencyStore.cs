using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace ApiPlatform.Platform.AspNetCore.Idempotency;

/// <summary>
/// <see cref="IDistributedCache"/>-backed idempotency store.  Entries survive process restarts
/// because they live in the cache backing (e.g. Redis, SQL) rather than in-process memory.
///
/// <para>
/// Use a shared, durable distributed cache (Redis, SQL Server, etc.) in multi-instance deployments.
/// For offline / dev use, the default registration uses <c>AddDistributedMemoryCache()</c>, which
/// keeps a single in-process cache that behaves like the memory store but serializes entries the
/// same way a real distributed cache would.
/// </para>
///
/// <para>
/// <strong>Atomicity:</strong> <see cref="TryBeginAsync"/> performs a best-effort check-then-set.
/// Without compare-and-swap semantics in <see cref="IDistributedCache"/> this is not perfectly
/// atomic across multiple hosts, but it is sufficient for single-node dev/test caches and
/// dramatically reduces duplicate executions in production.  The <see cref="InMemoryIdempotencyStore"/>
/// provides fully atomic set-if-absent for single-instance deployments where exact once-execution
/// is required.
/// </para>
/// </summary>
public sealed class DistributedCacheIdempotencyStore : IIdempotencyStore
{
    // Byte prefix markers stored as the first byte of each cache value.
    // Sentinel (0x01) = slot claimed by TryBeginAsync, not yet complete.
    // Entry  (0x02) = completed; the remaining bytes are UTF-8 JSON of IdempotencyEntry.
    private const byte SentinelMarker = 0x01;
    private const byte EntryMarker    = 0x02;

    private static readonly DistributedCacheEntryOptions NeverExpire = new();

    private readonly IDistributedCache _cache;

    public DistributedCacheIdempotencyStore(IDistributedCache cache) => _cache = cache;

    /// <inheritdoc/>
    public async ValueTask<IdempotencyEntry?> GetAsync(string key, CancellationToken ct = default)
    {
        var bytes = await _cache.GetAsync(key, ct).ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0) return null;

        // Sentinel: slot is claimed but the winner has not yet written the real entry.
        // Callers that receive null should treat this as "not ready".
        if (bytes[0] == SentinelMarker) return null;
        if (bytes[0] != EntryMarker)    return null;

        return JsonSerializer.Deserialize<IdempotencyEntry>(bytes.AsSpan(1));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Best-effort: reads the cache, and if no entry exists, writes a sentinel to claim the
    /// slot.  This is NOT atomically guaranteed across multiple hosts (no CAS primitive on
    /// <see cref="IDistributedCache"/>), but it is effective for single-process dev caches
    /// and substantially reduces duplicate executions elsewhere.
    /// </remarks>
    public async ValueTask<bool> TryBeginAsync(string key, CancellationToken ct = default)
    {
        var existing = await _cache.GetAsync(key, ct).ConfigureAwait(false);
        if (existing is not null) return false;

        await _cache.SetAsync(key, [SentinelMarker], NeverExpire, ct).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask SetAsync(string key, IdempotencyEntry entry, CancellationToken ct = default)
    {
        var json  = JsonSerializer.SerializeToUtf8Bytes(entry);
        var bytes = new byte[json.Length + 1];
        bytes[0] = EntryMarker;
        json.CopyTo(bytes, 1);
        await _cache.SetAsync(key, bytes, NeverExpire, ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    /// <remarks>Removes the sentinel so the slot is released and future requests can retry.</remarks>
    public async ValueTask AbandonAsync(string key, CancellationToken ct = default)
        => await _cache.RemoveAsync(key, ct).ConfigureAwait(false);
}
