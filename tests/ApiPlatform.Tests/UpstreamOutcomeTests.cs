using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ApiPlatform.Contracts;
using ApiPlatform.Integration.Acl;
using ApiPlatform.Platform.Errors;
using ApiPlatform.Platform.Results;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Tests;

/// <summary>
/// Verifies that vendor outages surface as honest 502/503 problem-details responses —
/// not as 404, empty lists, or generic 500s — at every layer of the stack.
///
/// Layers covered:
///   1. <see cref="Result{T}"/> type shape and factory statics
///   2. <see cref="VendorExecution"/> exception classifier (unit)
///   3. <see cref="RoutingAccountSource"/> fail-fast aggregation (unit)
///   4. HTTP endpoint → 502/503 problem+json via <see cref="WebApplicationFactory{TEntryPoint}"/> (integration)
/// </summary>
public class UpstreamOutcomeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    // Canonical ids from the existing seed data
    private const string DepositId = "f47ac10b-58cc-4372-a567-0e02b2c3d479";
    private const string UnknownId = "00000000-0000-0000-0000-000000000099";

    public UpstreamOutcomeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 1. Result<T> type shape
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Result_Success_IsSuccessTrue_ValueAccessible()
    {
        var r = Result<string>.Success("hello");

        Assert.True(r.IsSuccess);
        Assert.Equal(UpstreamOutcome.Success, r.Outcome);
        Assert.Equal("hello", r.Value);
        Assert.Null(r.Reason);
    }

    [Fact]
    public void Result_NotFound_OutcomeCorrect_ValueThrows()
    {
        var r = Result<string>.NotFound();

        Assert.False(r.IsSuccess);
        Assert.Equal(UpstreamOutcome.NotFound, r.Outcome);
        Assert.Throws<InvalidOperationException>(() => r.Value);
    }

    [Fact]
    public void Result_Transient_CarriesReason()
    {
        const string reason = "request timed out";
        var r = Result<int>.Transient(reason);

        Assert.Equal(UpstreamOutcome.Transient, r.Outcome);
        Assert.Equal(reason, r.Reason);
        Assert.False(r.IsSuccess);
    }

    [Fact]
    public void Result_VendorError_CarriesReason()
    {
        const string reason = "stub internal error";
        var r = Result<int>.VendorError(reason);

        Assert.Equal(UpstreamOutcome.VendorError, r.Outcome);
        Assert.Equal(reason, r.Reason);
    }

    [Fact]
    public void Result_Unauthorized_OutcomeCorrect()
    {
        var r = Result<string>.Unauthorized();
        Assert.Equal(UpstreamOutcome.Unauthorized, r.Outcome);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 2. VendorExecution classifier
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task VendorExecution_SuccessfulCall_ReturnsSuccessResult()
    {
        var result = await VendorExecution.ExecuteAsync<string>(
            () => Task.FromResult("ok"));

        Assert.True(result.IsSuccess);
        Assert.Equal("ok", result.Value);
    }

    [Fact]
    public async Task VendorExecution_TimeoutException_ReturnsTransient()
    {
        var result = await VendorExecution.ExecuteAsync<string>(
            () => Task.FromException<string>(new TimeoutException("stub timeout")));

        Assert.Equal(UpstreamOutcome.Transient, result.Outcome);
        Assert.Contains("stub timeout", result.Reason);
    }

    [Fact]
    public async Task VendorExecution_TaskCanceledException_NotCallerCancelled_ReturnsTransient()
    {
        // Simulate a vendor network timeout — TaskCanceledException but caller's token is NOT cancelled.
        var result = await VendorExecution.ExecuteAsync<string>(
            () => Task.FromException<string>(new TaskCanceledException("vendor timed out")),
            ct: CancellationToken.None);

        Assert.Equal(UpstreamOutcome.Transient, result.Outcome);
    }

    [Fact]
    public async Task VendorExecution_HttpRequestException_503_ReturnsTransient()
    {
        var ex = new HttpRequestException("service unavailable", null, HttpStatusCode.ServiceUnavailable);
        var result = await VendorExecution.ExecuteAsync<string>(
            () => Task.FromException<string>(ex));

        Assert.Equal(UpstreamOutcome.Transient, result.Outcome);
    }

    [Fact]
    public async Task VendorExecution_HttpRequestException_401_ReturnsUnauthorized()
    {
        var ex = new HttpRequestException("unauthorized", null, HttpStatusCode.Unauthorized);
        var result = await VendorExecution.ExecuteAsync<string>(
            () => Task.FromException<string>(ex));

        Assert.Equal(UpstreamOutcome.Unauthorized, result.Outcome);
    }

    [Fact]
    public async Task VendorExecution_HttpRequestException_404_ReturnsNotFound()
    {
        var ex = new HttpRequestException("not found", null, HttpStatusCode.NotFound);
        var result = await VendorExecution.ExecuteAsync<string>(
            () => Task.FromException<string>(ex));

        Assert.Equal(UpstreamOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task VendorExecution_ArbitraryException_ReturnsVendorError()
    {
        var result = await VendorExecution.ExecuteAsync<string>(
            () => Task.FromException<string>(new InvalidOperationException("something broke")));

        Assert.Equal(UpstreamOutcome.VendorError, result.Outcome);
        Assert.Contains("something broke", result.Reason);
    }

    [Fact]
    public async Task VendorExecution_CallerCancels_PropagatesOperationCanceled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            VendorExecution.ExecuteAsync<string>(
                () => Task.FromCanceled<string>(cts.Token),
                ct: cts.Token));
    }

    [Fact]
    public void VendorExecution_ThrowIfUpstreamError_TransientThrows()
    {
        var result = Result<string>.Transient("timeout");
        Assert.Throws<UpstreamUnavailableException>(() =>
            VendorExecution.ThrowIfUpstreamError(result, "SyntheticVendor"));
    }

    [Fact]
    public void VendorExecution_ThrowIfUpstreamError_SuccessPassesThrough()
    {
        var result = Result<string>.Success("ok");
        // Must NOT throw
        VendorExecution.ThrowIfUpstreamError(result, "SyntheticVendor");
    }

    [Fact]
    public void VendorExecution_ThrowIfUpstreamError_NotFoundPassesThrough()
    {
        var result = Result<string>.NotFound();
        // Must NOT throw — NotFound is handled by the caller (maps to null)
        VendorExecution.ThrowIfUpstreamError(result, "SyntheticVendor");
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 3. RoutingAccountSource fail-fast aggregation (unit)
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RoutingAccountSource_SingleVendorThrows_ListAccounts_ThrowsUpstreamUnavailable()
    {
        var failingVendor = new ThrowingAccountVendor(new TimeoutException("stub network timeout"));
        var source = new RoutingAccountSource(new[] { failingVendor });

        await Assert.ThrowsAsync<UpstreamUnavailableException>(() =>
            source.ListAccountsAsync(cursor: null, limit: 50));
    }

    [Fact]
    public async Task RoutingAccountSource_SecondVendorThrows_ListAccounts_ThrowsUpstreamUnavailable()
    {
        // First vendor is healthy; the aggregator must NOT silently return a partial list.
        var healthyVendor = new StaticAccountVendor("vendor-a",
            new Account { AccountId = Guid.NewGuid(), AccountType = AccountType.DEPOSIT, Currency = "USD" });
        var failingVendor = new ThrowingAccountVendor(new TimeoutException("second vendor down"));

        var source = new RoutingAccountSource(new IAccountVendor[] { healthyVendor, failingVendor });

        var ex = await Assert.ThrowsAsync<UpstreamUnavailableException>(() =>
            source.ListAccountsAsync(cursor: null, limit: 50));

        // The exception must carry the correct failure classification
        Assert.Equal(UpstreamOutcome.Transient, ex.Outcome);
        Assert.Equal("second vendor down", ex.VendorName);
    }

    [Fact]
    public async Task RoutingAccountSource_VendorThrows_GetAccount_ThrowsUpstreamUnavailable_NotNull()
    {
        // Previously a vendor exception could propagate as null (404). Now it must be a 502.
        var failingVendor = new ThrowingAccountVendor(new InvalidOperationException("stub error"));
        var source = new RoutingAccountSource(new[] { failingVendor });

        await Assert.ThrowsAsync<UpstreamUnavailableException>(() =>
            source.GetAccountAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task RoutingAccountSource_AllVendorsHealthy_GenuineNotFound_ReturnsNull()
    {
        // Healthy vendor with known accounts — looking up an unknown id must still return null (404).
        var knownId = Guid.NewGuid();
        var vendor = new StaticAccountVendor("vendor-a",
            new Account { AccountId = knownId, AccountType = AccountType.DEPOSIT, Currency = "USD" });
        var source = new RoutingAccountSource(new[] { vendor });

        var result = await source.GetAccountAsync(Guid.NewGuid()); // random unknown id

        Assert.Null(result);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // 4. HTTP endpoint integration via WebApplicationFactory
    // ══════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ListAccounts_FailingVendor_Returns502Or503_ProblemJson_NotEmpty()
    {
        // Inject a vendor that always times out alongside the real vendors.
        // RoutingAccountSource must NOT return a partial list — it must propagate the error.
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IAccountVendor>(
                    new ThrowingAccountVendor(new TimeoutException("synthetic timeout")))));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Scopes", "account.read");

        var response = await client.GetAsync("/v1/accounts");

        // Must NOT be 200 with a partial list, NOT 404, NOT 500-generic
        Assert.True(
            (int)response.StatusCode is 502 or 503,
            $"Expected 502 or 503 for a failing vendor; got {(int)response.StatusCode}");

        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.Equal("application/problem+json", contentType);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);
        Assert.True(
            body!["status"]?.GetValue<int>() is 502 or 503,
            "Problem Details status field must be 502 or 503");
        Assert.Equal(
            "https://apiplatform.dev/problems/upstream-unavailable",
            body["type"]?.GetValue<string>());
    }

    [Fact]
    public async Task GetAccount_FailingVendor_Returns502Or503_NotNotFound()
    {
        // A vendor failure must NOT render as 404.
        // The failing vendor is registered last (DI order). The routing source iterates ALL vendors
        // when looking up an ID that no existing healthy vendor owns, so the failing vendor IS invoked
        // and its error propagates honestly as 502/503 rather than null → 404.
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IAccountVendor>(
                    new ThrowingAccountVendor(new InvalidOperationException("vendor internal error")))));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Scopes", "account.read");

        // UnknownId is not owned by any healthy vendor, so the loop reaches the failing vendor.
        var response = await client.GetAsync($"/v1/accounts/{UnknownId}");

        Assert.True(
            (int)response.StatusCode is 502 or 503,
            $"Expected 502 or 503; got {(int)response.StatusCode} — vendor failure must not render as 404");

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(
            "https://apiplatform.dev/problems/upstream-unavailable",
            body!["type"]?.GetValue<string>());
    }

    [Fact]
    public async Task GetAccount_GenuineNotFound_StillReturns404()
    {
        // With all real (healthy) vendors registered, a genuinely missing account must still be 404.
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Scopes", "account.read");

        var response = await client.GetAsync($"/v1/accounts/{UnknownId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal(404, body!["status"]?.GetValue<int>());
    }

    [Fact]
    public async Task ListAccounts_AllVendorsHealthy_Returns200()
    {
        // Regression: success path must be unaffected by the new classifier.
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Scopes", "account.read");

        var response = await client.GetAsync("/v1/accounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        var data = body!["data"]?.AsArray();
        Assert.NotNull(data);
        Assert.True(data!.Count >= 3, "Expected at least 3 accounts from healthy vendors");
    }

    [Fact]
    public async Task UpstreamUnavailableException_Outcome_Transient_Produces503()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IAccountVendor>(
                    new ThrowingAccountVendor(new TimeoutException("synthetic timeout")))));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Scopes", "account.read");

        var response = await client.GetAsync("/v1/accounts");

        // TimeoutException → Transient → 503
        Assert.Equal(503, (int)response.StatusCode);
    }

    [Fact]
    public async Task UpstreamUnavailableException_Outcome_VendorError_Produces502()
    {
        var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IAccountVendor>(
                    new ThrowingAccountVendor(new InvalidOperationException("non-transient vendor error")))));

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Scopes", "account.read");

        var response = await client.GetAsync("/v1/accounts");

        // InvalidOperationException → VendorError → 502
        Assert.Equal(502, (int)response.StatusCode);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Stub helpers (test-only; obviously synthetic — no real system data)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// An IAccountVendor stub that always throws <paramref name="exceptionToThrow"/>.
    /// Injected to simulate vendor 500s, timeouts, and other outage conditions.
    /// </summary>
    private sealed class ThrowingAccountVendor(Exception exceptionToThrow) : IAccountVendor
    {
        public string SourceSystem => exceptionToThrow.Message;

        public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken ct = default)
            => Task.FromException<IReadOnlyList<Account>>(exceptionToThrow);

        public Task<IReadOnlyList<Transaction>> GetTransactionsAsync(Guid accountId, CancellationToken ct = default)
            => Task.FromException<IReadOnlyList<Transaction>>(exceptionToThrow);
    }

    /// <summary>
    /// An IAccountVendor stub that returns a fixed set of accounts.
    /// Used to verify that the healthy-vendor path still works correctly
    /// when another vendor in the list fails.
    /// </summary>
    private sealed class StaticAccountVendor(string sourceSystem, params Account[] accounts) : IAccountVendor
    {
        public string SourceSystem => sourceSystem;

        public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Account>>(accounts);

        public Task<IReadOnlyList<Transaction>> GetTransactionsAsync(Guid accountId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Transaction>>(Array.Empty<Transaction>());
    }
}
