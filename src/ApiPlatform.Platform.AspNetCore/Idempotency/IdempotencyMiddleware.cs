using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace ApiPlatform.Platform.AspNetCore.Idempotency;

/// <summary>
/// For unsafe methods (POST, PUT, PATCH) carrying an <c>Idempotency-Key</c> header:
/// <list type="bullet">
///   <item>
///     If the key has been seen by THIS principal, replay the stored response and add
///     <c>Idempotency-Replayed: true</c>.
///   </item>
///   <item>
///     Otherwise atomically claim the slot via <see cref="IIdempotencyStore.TryBeginAsync"/>,
///     capture the completed response, store it, and serve it normally.
///   </item>
///   <item>
///     Concurrent requests from the same principal with the same key are serialised: the loser
///     waits for the winner to finish and then replays the winner's stored response, guaranteeing
///     exactly one handler execution per (method, path, principal, Idempotency-Key) tuple.
///   </item>
/// </list>
/// Requests without the header pass through untouched.  Two different authenticated principals
/// sharing the same Idempotency-Key and route are fully isolated.
///
/// <para>
/// <strong>Principal derivation</strong> mirrors <c>HttpAuditContext.Actor</c>: JWT <c>sub</c>
/// claim, then <c>IIdentity.Name</c>, then the literal <c>"anonymous"</c>.
/// </para>
/// </summary>
public sealed class IdempotencyMiddleware
{
    private const string IdempotencyKeyHeader     = "Idempotency-Key";
    private const string IdempotencyReplayedHeader = "Idempotency-Replayed";

    private static readonly HashSet<string> UnsafeMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH" };

    private readonly RequestDelegate _next;
    private readonly IIdempotencyStore _store;

    public IdempotencyMiddleware(RequestDelegate next, IIdempotencyStore store)
    {
        _next  = next;
        _store = store;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!UnsafeMethods.Contains(ctx.Request.Method) ||
            !ctx.Request.Headers.TryGetValue(IdempotencyKeyHeader, out var keyValues))
        {
            await _next(ctx);
            return;
        }

        // Derive the principal using the same resolution order as HttpAuditContext.Actor:
        // sub claim → Identity.Name → "anonymous"
        var user      = ctx.User;
        var principal = user.FindFirst("sub")?.Value
                     ?? user.Identity?.Name
                     ?? "anonymous";

        var idempotencyKey = keyValues.ToString();
        var storeKey = $"{ctx.Request.Method}:{ctx.Request.Path}:{principal}:{idempotencyKey}";

        // --- Phase 1: check for an existing (or in-flight) completed entry ---
        // GetAsync on the in-memory store blocks if a winner is executing so losers
        // wait here rather than proceeding to TryBeginAsync.
        var existing = await _store.GetAsync(storeKey, ctx.RequestAborted);
        if (existing is not null)
        {
            await ReplayEntryAsync(ctx, existing);
            return;
        }

        // --- Phase 2: atomically claim the slot ---
        bool won = await _store.TryBeginAsync(storeKey, ctx.RequestAborted);
        if (!won)
        {
            // Lost the race (narrow window between GetAsync returning null and another caller
            // winning TryBeginAsync).  Wait for the winner's result; the in-memory store
            // blocks here on the TCS; the distributed store returns null immediately (best-effort).
            var raceEntry = await _store.GetAsync(storeKey, ctx.RequestAborted);
            if (raceEntry is not null)
                await ReplayEntryAsync(ctx, raceEntry);
            // else: winner failed / abandoned — fall through (best effort).
            return;
        }

        // --- Phase 3: winner — capture the response ---
        var originalBody = ctx.Response.Body;
        using var buffer = new MemoryStream();
        ctx.Response.Body = buffer;

        IdempotencyEntry? captured = null;
        try
        {
            await _next(ctx);

            var capturedBody = buffer.ToArray();
            captured = new IdempotencyEntry(
                ctx.Response.StatusCode,
                ctx.Response.ContentType ?? string.Empty,
                capturedBody);
        }
        finally
        {
            // Always restore the original body stream before any further writes.
            ctx.Response.Body = originalBody;

            if (captured is not null)
            {
                // Store the completed entry and unblock any waiting losers.
                // Use CancellationToken.None so a cancelled request does not prevent
                // the entry from being stored (it is already captured and complete).
                await _store.SetAsync(storeKey, captured, CancellationToken.None);

                if (captured.Body.Length > 0)
                    await originalBody.WriteAsync(captured.Body, ctx.RequestAborted);
            }
            else
            {
                // Handler threw; release the slot so losers are not blocked indefinitely.
                await _store.AbandonAsync(storeKey, CancellationToken.None);
            }
        }
    }

    private static async Task ReplayEntryAsync(HttpContext ctx, IdempotencyEntry entry)
    {
        ctx.Response.StatusCode  = entry.StatusCode;
        ctx.Response.ContentType = entry.ContentType;
        ctx.Response.Headers[IdempotencyReplayedHeader] = "true";
        if (entry.Body.Length > 0)
            await ctx.Response.Body.WriteAsync(entry.Body, ctx.RequestAborted);
    }
}
