using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiPlatform.Tests;

/// <summary>
/// Integration tests for the event-driven slice:
///   POST /v1/accounts/{id}/touch    — trigger endpoint
///   OPTIONS /hooks/events            — Event Grid handshake
///   POST /hooks/events               — webhook receiver
///   GET  /hooks/events/log           — received-event log
/// </summary>
public class EventingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    // Seeded account IDs from CoreBankingAccountSource / CardsPlatformAccountSource
    private const string DepositId = "f47ac10b-58cc-4372-a567-0e02b2c3d479";
    private const string UnknownId = "00000000-0000-0000-0000-000000000001";

    public EventingTests(WebApplicationFactory<Program> factory)
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

    // ── 1. POST /touch with event.publish → 202 + EventAccepted body ─────────

    [Fact]
    public async Task TouchAccount_WithEventPublishScope_Returns202AndEventAcceptedBody()
    {
        var client   = CreateClient("event.publish");
        var response = await client.PostAsync($"/v1/accounts/{DepositId}/touch", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);
        Assert.Equal("northwind.account.touched", body!["eventType"]?.GetValue<string>());
        Assert.Equal(DepositId,                   body!["id"]?.GetValue<string>());
        Assert.Equal("PUBLISHED",                 body!["status"]?.GetValue<string>());
    }

    // ── 2. POST /touch without event.publish → 403 problem+json ──────────────

    [Fact]
    public async Task TouchAccount_MissingScope_Returns403ProblemJson()
    {
        var client   = CreateClient("account.read");   // wrong scope
        var response = await client.PostAsync($"/v1/accounts/{DepositId}/touch", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);
        Assert.Equal(403, body!["status"]?.GetValue<int>());
    }

    // ── 3. POST /touch with unknown account id → 404 Problem Details ──────────

    [Fact]
    public async Task TouchAccount_UnknownId_Returns404ProblemDetails()
    {
        var client   = CreateClient("event.publish");
        var response = await client.PostAsync($"/v1/accounts/{UnknownId}/touch", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);
        Assert.Equal(404, body!["status"]?.GetValue<int>());
    }

    // ── 4. OPTIONS /hooks/events with WebHook-Request-Origin → handshake ──────

    [Fact]
    public async Task HooksEvents_Options_WithOriginHeader_Returns200AndAllowedOriginHeader()
    {
        var client  = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/hooks/events");
        request.Headers.TryAddWithoutValidation("WebHook-Request-Origin", "eventgrid.azure.net");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(
            response.Headers.TryGetValues("WebHook-Allowed-Origin", out var values),
            "Response must contain WebHook-Allowed-Origin header");
        Assert.Equal("*", values!.First());
    }

    // ── 5. POST /hooks/events with CloudEvent → 200, GET /log shows entry ─────

    [Fact]
    public async Task HooksEvents_Post_CloudEvent_Then_LogShowsEntry()
    {
        var client    = _factory.CreateClient();
        var subjectId = Guid.NewGuid().ToString();

        var cloudEventJson = $$"""
            {
                "specversion": "1.0",
                "type": "northwind.account.touched",
                "source": "/northwind/api",
                "id": "{{Guid.NewGuid()}}",
                "subject": "{{subjectId}}",
                "time": "{{DateTimeOffset.UtcNow:O}}",
                "datacontenttype": "application/json"
            }
            """;

        var content  = new StringContent(cloudEventJson, Encoding.UTF8, "application/cloudevents+json");
        var response = await client.PostAsync("/hooks/events", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var postBody = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(postBody);
        Assert.Equal(1, postBody!["received"]?.GetValue<int>());

        // GET the log and confirm the entry is there
        var logResponse = await client.GetAsync("/hooks/events/log");
        Assert.Equal(HttpStatusCode.OK, logResponse.StatusCode);

        var log = await logResponse.Content.ReadFromJsonAsync<JsonArray>();
        Assert.NotNull(log);
        Assert.True(log!.Count >= 1, "Log should contain at least one entry");

        // The entry we just posted should be first (most-recent first)
        var entry = log[0]!.AsObject();
        Assert.Equal("northwind.account.touched", entry["type"]?.GetValue<string>());
        Assert.Equal(subjectId, entry["id"]?.GetValue<string>());
    }
}
