using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiPlatform.Tests;

/// <summary>
/// Integration tests for the Accounts API using WebApplicationFactory.
/// The X-Scopes header drives scope-based auth (no real token required).
/// </summary>
public class AccountsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    // Seed IDs from spec / CoreBankingAccountSource
    private const string DepositId = "f47ac10b-58cc-4372-a567-0e02b2c3d479";
    private const string CreditId  = "7c9e6679-7425-40de-944b-e07fc1f90ae7";
    private const string UnknownId = "00000000-0000-0000-0000-000000000001";

    public AccountsApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private HttpClient CreateClient(params string[] scopes)
    {
        var client = _factory.CreateClient();
        if (scopes.Length > 0)
            client.DefaultRequestHeaders.Add("X-Scopes", string.Join(" ", scopes));
        return client;
    }

    // ── 1. GET /v1/accounts returns 200 + seeded accounts with basic fields ──

    [Fact]
    public async Task ListAccounts_WithAccountRead_Returns200AndAccounts()
    {
        var client = CreateClient("account.read");
        var response = await client.GetAsync("/v1/accounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);

        var data = body!["data"]?.AsArray();
        Assert.NotNull(data);
        Assert.True(data!.Count >= 3, $"Expected at least 3 accounts but got {data.Count}");

        // Verify at least one account has basic fields
        var first = data[0]?.AsObject();
        Assert.NotNull(first!["accountId"]);
        Assert.NotNull(first!["accountType"]);
        Assert.NotNull(first!["status"]);
        Assert.NotNull(first!["currency"]);
    }

    // ── 2. account.read only → detail objects OMITTED ────────────────────────

    [Fact]
    public async Task ListAccounts_WithOnlyAccountRead_DetailObjectsOmitted()
    {
        var client = CreateClient("account.read");
        var response = await client.GetAsync("/v1/accounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        var data = body!["data"]!.AsArray();

        foreach (var item in data)
        {
            var obj = item!.AsObject();
            Assert.False(obj.ContainsKey("depositAccount"), "depositAccount should be omitted without account.detailed.read");
            Assert.False(obj.ContainsKey("creditAccount"),  "creditAccount should be omitted without account.detailed.read");
            Assert.False(obj.ContainsKey("loanAccount"),    "loanAccount should be omitted without account.detailed.read");
        }
    }

    // ── 3. account.read + account.detailed.read → detail objects PRESENT ─────

    [Fact]
    public async Task ListAccounts_WithDetailedRead_DetailObjectsPresent()
    {
        var client = CreateClient("account.read", "account.detailed.read");
        var response = await client.GetAsync("/v1/accounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        var data = body!["data"]!.AsArray();

        // At least one account in the seed set should have a detail object
        bool foundAny = data.Any(item =>
        {
            var obj = item!.AsObject();
            return obj.ContainsKey("depositAccount")
                || obj.ContainsKey("creditAccount")
                || obj.ContainsKey("loanAccount");
        });

        Assert.True(foundAny, "Expected at least one account to have a type-specific detail object when account.detailed.read scope is present");
    }

    // ── 4. Missing scope → 403 with application/problem+json body ────────────

    [Fact]
    public async Task ListAccounts_MissingScope_Returns403ProblemDetails()
    {
        var client = CreateClient(/* no scopes */);
        var response = await client.GetAsync("/v1/accounts");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.Equal("application/problem+json", contentType);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);
        Assert.Equal(403, body!["status"]?.GetValue<int>());
    }

    [Fact]
    public async Task ListTransactions_MissingScope_Returns403ProblemDetails()
    {
        var client = CreateClient("account.read"); // has account.read but NOT transaction.read
        var response = await client.GetAsync($"/v1/accounts/{CreditId}/transactions");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.Equal("application/problem+json", contentType);
    }

    // ── 5a. GET /v1/accounts/{id} 200 for seeded id ──────────────────────────

    [Fact]
    public async Task GetAccount_KnownId_Returns200()
    {
        var client = CreateClient("account.read");
        var response = await client.GetAsync($"/v1/accounts/{DepositId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);
        Assert.Equal(DepositId, body!["accountId"]?.GetValue<string>());
        Assert.Equal("DEPOSIT", body!["accountType"]?.GetValue<string>());
    }

    // ── 5b. GET /v1/accounts/{id} 404 (Problem Details) for unknown id ───────

    [Fact]
    public async Task GetAccount_UnknownId_Returns404ProblemDetails()
    {
        var client = CreateClient("account.read");
        var response = await client.GetAsync($"/v1/accounts/{UnknownId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.Equal("application/problem+json", contentType);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);
        Assert.Equal(404, body!["status"]?.GetValue<int>());
    }

    // ── 6. GET /v1/accounts/{id}/transactions returns 200 + seeded txns ──────

    [Fact]
    public async Task ListTransactions_KnownAccount_Returns200AndTransactions()
    {
        var client = CreateClient("transaction.read");
        var response = await client.GetAsync($"/v1/accounts/{CreditId}/transactions");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);

        var data = body!["data"]?.AsArray();
        Assert.NotNull(data);
        Assert.True(data!.Count >= 2, $"Expected at least 2 transactions for the credit account, got {data.Count}");

        var first = data[0]!.AsObject();
        Assert.NotNull(first["transactionId"]);
        Assert.NotNull(first["amount"]);
        Assert.Equal(CreditId, first["accountId"]?.GetValue<string>());
    }

    // ── 7. Accept: text/csv on list returns CSV content-type ─────────────────

    [Fact]
    public async Task ListAccounts_AcceptCsv_ReturnsCsvContentType()
    {
        var client = CreateClient("account.read");
        client.DefaultRequestHeaders.Add("Accept", "text/csv");

        var response = await client.GetAsync("/v1/accounts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.Equal("text/csv", contentType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(body), "CSV body should not be empty");
        // Should have a header row
        Assert.Contains("AccountId", body, StringComparison.OrdinalIgnoreCase);
    }

    // ── Bonus: health check ───────────────────────────────────────────────────

    [Fact]
    public async Task HealthCheck_Returns200()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
