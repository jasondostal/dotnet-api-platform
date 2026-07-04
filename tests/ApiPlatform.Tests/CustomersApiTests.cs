using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiPlatform.Tests;

/// <summary>
/// Integration tests for the Customers API using WebApplicationFactory.
/// The X-Scopes header drives scope-based auth (no real token required).
/// </summary>
public class CustomersApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    // Seed IDs from spec / CoreBankingCustomerSource
    private const string AveryId   = "3f1a7c20-9b54-4e11-a8d3-1c2b3a4d5e6f";
    private const string UnknownId = "00000000-0000-0000-0000-000000000002";

    public CustomersApiTests(WebApplicationFactory<Program> factory)
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

    // ── 1. GET /v1/customers returns 200 + seeded customers ──────────────────

    [Fact]
    public async Task ListCustomers_WithCustomerRead_Returns200AndCustomers()
    {
        var client = CreateClient("customer.read");
        var response = await client.GetAsync("/v1/customers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);

        var data = body!["data"]?.AsArray();
        Assert.NotNull(data);
        Assert.True(data!.Count >= 3, $"Expected at least 3 customers but got {data.Count}");

        var first = data[0]?.AsObject();
        Assert.NotNull(first!["customerId"]);
        Assert.NotNull(first!["name"]);
        Assert.NotNull(first!["status"]);
    }

    // ── 2. customer.read only → contact object OMITTED ───────────────────────

    [Fact]
    public async Task ListCustomers_WithOnlyCustomerRead_ContactOmitted()
    {
        var client = CreateClient("customer.read");
        var response = await client.GetAsync("/v1/customers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        var data = body!["data"]!.AsArray();

        foreach (var item in data)
        {
            var obj = item!.AsObject();
            Assert.False(obj.ContainsKey("contact"), "contact should be omitted without contact.read");
        }
    }

    // ── 3. customer.read + contact.read → contact object PRESENT ─────────────

    [Fact]
    public async Task ListCustomers_WithContactRead_ContactPresent()
    {
        var client = CreateClient("customer.read", "contact.read");
        var response = await client.GetAsync("/v1/customers");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        var data = body!["data"]!.AsArray();

        bool foundAny = data.Any(item => item!.AsObject().ContainsKey("contact"));
        Assert.True(foundAny, "Expected at least one customer to have a contact object when contact.read scope is present");
    }

    // ── 4. Missing scope → 403 with application/problem+json ─────────────────

    [Fact]
    public async Task ListCustomers_MissingScope_Returns403ProblemDetails()
    {
        var client = CreateClient(/* no scopes */);
        var response = await client.GetAsync("/v1/customers");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.Equal("application/problem+json", contentType);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);
        Assert.Equal(403, body!["status"]?.GetValue<int>());
    }

    // ── 5a. GET /v1/customers/{id} 200 for seeded id ─────────────────────────

    [Fact]
    public async Task GetCustomer_KnownId_Returns200()
    {
        var client = CreateClient("customer.read");
        var response = await client.GetAsync($"/v1/customers/{AveryId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);
        Assert.Equal(AveryId, body!["customerId"]?.GetValue<string>());
        Assert.Equal("ACTIVE", body!["status"]?.GetValue<string>());

        // Verify name sub-object
        var name = body!["name"]?.AsObject();
        Assert.NotNull(name);
        Assert.Equal("Avery", name!["first"]?.GetValue<string>());
        Assert.Equal("Lindgren", name!["last"]?.GetValue<string>());
    }

    // ── 5b. GET /v1/customers/{id} contact omitted without contact.read ───────

    [Fact]
    public async Task GetCustomer_WithOnlyCustomerRead_ContactOmitted()
    {
        var client = CreateClient("customer.read");
        var response = await client.GetAsync($"/v1/customers/{AveryId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);
        Assert.False(body!.AsObject().ContainsKey("contact"), "contact should be omitted without contact.read");
    }

    // ── 5c. GET /v1/customers/{id} contact present with contact.read ──────────

    [Fact]
    public async Task GetCustomer_WithContactRead_ContactPresent()
    {
        var client = CreateClient("customer.read", "contact.read");
        var response = await client.GetAsync($"/v1/customers/{AveryId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);
        Assert.True(body!.AsObject().ContainsKey("contact"), "contact should be present with contact.read");

        var contact = body!["contact"]?.AsObject();
        Assert.NotNull(contact);
        Assert.True(contact!.ContainsKey("emails"), "contact.emails should be present");
    }

    // ── 5d. GET /v1/customers/{id} 404 Problem Details for unknown id ─────────

    [Fact]
    public async Task GetCustomer_UnknownId_Returns404ProblemDetails()
    {
        var client = CreateClient("customer.read");
        var response = await client.GetAsync($"/v1/customers/{UnknownId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var contentType = response.Content.Headers.ContentType?.MediaType;
        Assert.Equal("application/problem+json", contentType);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);
        Assert.Equal(404, body!["status"]?.GetValue<int>());
    }
}
