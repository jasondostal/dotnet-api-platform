using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using ApiPlatform.Platform.AspNetCore.Auth;
using ApiPlatform.Platform.AspNetCore.Idempotency;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApiPlatform.Tests;

/// <summary>
/// Covers the three hardening properties added to the idempotency layer:
/// <list type="number">
///   <item>Principal isolation: two different authenticated callers sharing the same key and
///     route do NOT cross responses.</item>
///   <item>Atomic set-if-absent: concurrent same-principal+same-key requests produce exactly
///     one handler execution; the loser replays the winner's result.</item>
///   <item>Durable round-trip: the <c>Distributed</c> store survives an instance restart
///     (new store object over the same IDistributedCache backing).</item>
/// </list>
/// </summary>
public class IdempotencyAdvancedTests
{
    // ── Test-only auth handler that reads X-Principal header as identity name ──────────────

    /// <summary>
    /// Authentication handler used in tests that need distinct principals without a full
    /// JWT stack.  The <c>X-Principal</c> header value becomes <c>Identity.Name</c>, which
    /// the idempotency middleware reads as the principal (matching HttpAuditContext.Actor's
    /// fallback when no "sub" claim is present).
    /// </summary>
    private sealed class PrincipalHeaderHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "PrincipalHeader";

        public PrincipalHeaderHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder) { }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var name     = Request.Headers["X-Principal"].FirstOrDefault() ?? "anonymous";
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], SchemeName);
            var ticket   = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal self-contained test server: idempotency middleware only, with the
    /// <see cref="PrincipalHeaderHandler"/> for principal injection.  No scope policies.
    /// </summary>
    private static async Task<(WebApplication App, HttpClient Client)> BuildTestServerAsync(
        RequestDelegate handler,
        string route = "/test")
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Development",
        });
        builder.WebHost.UseTestServer();

        builder.Services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        builder.Services
            .AddAuthentication(PrincipalHeaderHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, PrincipalHeaderHandler>(
                PrincipalHeaderHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<IdempotencyMiddleware>();
        app.MapPost(route, handler);

        await app.StartAsync();

        return (app, app.GetTestClient());
    }

    // ── Test 1: principal isolation ───────────────────────────────────────────────────────

    /// <summary>
    /// Two different authenticated principals that submit the same Idempotency-Key on the
    /// same route must NOT share stored responses.  Each principal gets its own response
    /// lineage and must not receive a replayed header from the other principal's execution.
    /// </summary>
    [Fact]
    public async Task TwoDifferentPrincipals_SameIdempotencyKey_ResponsesDoNotCross()
    {
        var handlerCallCount = 0;
        var (app, client) = await BuildTestServerAsync(
            async (HttpContext ctx) =>
            {
                Interlocked.Increment(ref handlerCallCount);
                var principal = ctx.User.Identity?.Name ?? "unknown";
                ctx.Response.ContentType = "text/plain";
                await ctx.Response.WriteAsync(principal);
            });

        await using (app)
        {
            var sharedKey = Guid.NewGuid().ToString();

            // ── Alice: first request (should execute) ──────────────────────────
            var aliceReq1 = new HttpRequestMessage(HttpMethod.Post, "/test");
            aliceReq1.Headers.Add("X-Principal", "alice");
            aliceReq1.Headers.Add("Idempotency-Key", sharedKey);
            var aliceRes1 = await client.SendAsync(aliceReq1);

            Assert.Equal(HttpStatusCode.OK, aliceRes1.StatusCode);
            Assert.False(aliceRes1.Headers.Contains("Idempotency-Replayed"),
                "Alice's first request must NOT carry Idempotency-Replayed");
            Assert.Equal("alice", await aliceRes1.Content.ReadAsStringAsync());

            // ── Bob: same key, DIFFERENT principal — must execute independently ─
            var bobReq1 = new HttpRequestMessage(HttpMethod.Post, "/test");
            bobReq1.Headers.Add("X-Principal", "bob");
            bobReq1.Headers.Add("Idempotency-Key", sharedKey);
            var bobRes1 = await client.SendAsync(bobReq1);

            Assert.Equal(HttpStatusCode.OK, bobRes1.StatusCode);
            Assert.False(bobRes1.Headers.Contains("Idempotency-Replayed"),
                "Bob's first request must NOT carry Idempotency-Replayed — different principal");
            Assert.Equal("bob", await bobRes1.Content.ReadAsStringAsync());

            // ── Alice: second request — same key, same principal — must replay ──
            var aliceReq2 = new HttpRequestMessage(HttpMethod.Post, "/test");
            aliceReq2.Headers.Add("X-Principal", "alice");
            aliceReq2.Headers.Add("Idempotency-Key", sharedKey);
            var aliceRes2 = await client.SendAsync(aliceReq2);

            Assert.Equal(HttpStatusCode.OK, aliceRes2.StatusCode);
            Assert.True(aliceRes2.Headers.Contains("Idempotency-Replayed"),
                "Alice's second request must carry Idempotency-Replayed");
            // The replayed body is Alice's, not Bob's
            Assert.Equal("alice", await aliceRes2.Content.ReadAsStringAsync());

            // Handler ran once per principal (2 total), not 3.
            Assert.Equal(2, handlerCallCount);
        }
    }

    // ── Test 2: atomic set-if-absent / exactly-one-execution ─────────────────────────────

    /// <summary>
    /// Two concurrent requests from the same principal with the same Idempotency-Key must
    /// result in exactly one handler invocation.  The loser blocks until the winner completes
    /// and then replays the winner's stored result.
    ///
    /// Determinism is achieved via a barrier (<see cref="TaskCompletionSource"/>):
    /// the first request reaches the handler (winner), releases <c>handlerReached</c>,
    /// then blocks on <c>barrier</c>.  Only after <c>handlerReached</c> fires is the
    /// second (loser) request sent — guaranteeing the winner has claimed the slot before
    /// the loser's <see cref="IIdempotencyStore.GetAsync"/> runs.
    /// </summary>
    [Fact]
    public async Task ConcurrentRequests_SamePrincipalAndKey_ExactlyOneHandlerExecution()
    {
        var handlerCount  = 0;
        var handlerReached = new SemaphoreSlim(0, 1);
        var barrier        = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var (app, client) = await BuildTestServerAsync(
            async (HttpContext ctx) =>
            {
                Interlocked.Increment(ref handlerCount);
                handlerReached.Release();          // signal: we are inside the handler
                await barrier.Task;                // block until the test releases us
                ctx.Response.ContentType = "application/json";
                await ctx.Response.WriteAsync("{\"ok\":true}");
            });

        await using (app)
        {
            var key = Guid.NewGuid().ToString();

            // Send the first (winner) request and wait until it is inside the handler.
            var req1 = new HttpRequestMessage(HttpMethod.Post, "/test");
            req1.Headers.Add("X-Principal", "alice");
            req1.Headers.Add("Idempotency-Key", key);
            var task1 = client.SendAsync(req1);

            await handlerReached.WaitAsync(TimeSpan.FromSeconds(10));

            // At this point: winner has called TryBeginAsync and is blocking inside the handler.
            // Send the loser request — its GetAsync will block on the in-flight TCS.
            var req2 = new HttpRequestMessage(HttpMethod.Post, "/test");
            req2.Headers.Add("X-Principal", "alice");
            req2.Headers.Add("Idempotency-Key", key);
            var task2 = client.SendAsync(req2);

            // Give the loser a moment to reach GetAsync and start awaiting the TCS.
            await Task.Delay(50);

            // Release the barrier — winner completes, calls SetAsync, TCS fires, loser unblocks.
            barrier.SetResult();

            var res1 = await task1.WaitAsync(TimeSpan.FromSeconds(10));
            var res2 = await task2.WaitAsync(TimeSpan.FromSeconds(10));

            // Handler ran exactly once.
            Assert.Equal(1, handlerCount);

            // Exactly one response is the original; the other is the replay.
            bool r1Replayed = res1.Headers.Contains("Idempotency-Replayed");
            bool r2Replayed = res2.Headers.Contains("Idempotency-Replayed");

            Assert.True(r1Replayed ^ r2Replayed,
                "Exactly one of the two responses must carry Idempotency-Replayed");
        }
    }

    // ── Test 3: durable round-trip (Distributed store) ───────────────────────────────────

    /// <summary>
    /// The <see cref="DistributedCacheIdempotencyStore"/> must survive a simulated
    /// "process restart" — that is, a second store instance that shares the same underlying
    /// <see cref="IDistributedCache"/> object can retrieve entries written by the first
    /// instance.  This validates that entries are serialised to the cache (not held in
    /// process-local state) and that the key/format is stable across instances.
    /// </summary>
    [Fact]
    public async Task DistributedStore_RoundTripsEntry_AcrossNewStoreInstance()
    {
        // Shared cache — simulates the same Redis / SQL backing surviving a restart.
        var cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));

        var entry = new IdempotencyEntry(202, "application/json", "{\"status\":\"ok\"}"u8.ToArray());
        const string key = "POST:/test/round-trip:alice:abc-123";

        // ── Instance A: write ─────────────────────────────────────────────────
        var storeA = new DistributedCacheIdempotencyStore(cache);
        await storeA.SetAsync(key, entry);

        // ── Instance B: brand-new object, same cache — read ───────────────────
        var storeB = new DistributedCacheIdempotencyStore(cache);
        var retrieved = await storeB.GetAsync(key);

        Assert.NotNull(retrieved);
        Assert.Equal(entry.StatusCode,   retrieved.StatusCode);
        Assert.Equal(entry.ContentType,  retrieved.ContentType);
        Assert.Equal(entry.Body,         retrieved.Body);
    }

    /// <summary>
    /// <see cref="DistributedCacheIdempotencyStore.TryBeginAsync"/> must return
    /// <c>true</c> (winner) for the first caller and <c>false</c> (slot already claimed)
    /// for the same key on the same instance.
    /// </summary>
    [Fact]
    public async Task DistributedStore_TryBeginAsync_FirstCallerWins_SecondLoses()
    {
        var cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));
        var store = new DistributedCacheIdempotencyStore(cache);
        const string key = "test-begin-once";

        bool won1 = await store.TryBeginAsync(key);
        bool won2 = await store.TryBeginAsync(key);

        Assert.True(won1,  "First caller must win");
        Assert.False(won2, "Second caller must lose (slot already claimed)");
    }
}
