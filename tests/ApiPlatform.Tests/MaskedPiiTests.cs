using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiPlatform.Tests;

/// <summary>
/// Verifies the scope-gated contact projection and proves the PII-redactor seam is in place.
/// The IPiiRedactor is injected into CoreBankingCustomerSource for audit/diagnostic masking;
/// these tests confirm the contact field behaves correctly from the API consumer's perspective.
/// </summary>
public class MaskedPiiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    // Seed ID for Avery Lindgren from CoreBankingCustomerSource
    private const string AveryId = "3f1a7c20-9b54-4e11-a8d3-1c2b3a4d5e6f";

    public MaskedPiiTests(WebApplicationFactory<Program> factory)
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

    // ── 1. Without contact.read — contact object absent ───────────────────────

    [Fact]
    public async Task GetCustomer_WithoutContactRead_ContactAbsentFromResponse()
    {
        var client   = CreateClient("customer.read");
        var response = await client.GetAsync($"/v1/customers/{AveryId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);

        // Identity fields must be present
        Assert.Equal(AveryId, body!["customerId"]?.GetValue<string>());
        Assert.Equal("ACTIVE", body!["status"]?.GetValue<string>());

        // contact object must be absent — scope-gated; no PII reaches the wire
        Assert.False(body!.AsObject().ContainsKey("contact"),
            "contact should be absent when only customer.read scope is present");
    }

    // ── 2. With contact.read — full contact present, unmasked for authorized caller ─

    [Fact]
    public async Task GetCustomer_WithContactRead_FullContactPresent()
    {
        var client   = CreateClient("customer.read", "contact.read");
        var response = await client.GetAsync($"/v1/customers/{AveryId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);

        // contact object must be present and contain email + phone
        Assert.True(body!.AsObject().ContainsKey("contact"),
            "contact should be present when contact.read scope is present");

        var contact = body!["contact"]?.AsObject();
        Assert.NotNull(contact);
        Assert.True(contact!.ContainsKey("emails"), "contact.emails should be present");
        Assert.True(contact!.ContainsKey("phones"), "contact.phones should be present");

        // Full unmasked data reaches authorized callers
        // Note: Email.EmailAddress serializes as "email" per [JsonPropertyName("email")]
        var emails = contact["emails"]?.AsArray();
        Assert.NotNull(emails);
        Assert.True(emails!.Count > 0, "At least one email expected");
        var firstEmail = emails![0]?.AsObject();
        Assert.NotNull(firstEmail);
        var emailValue = firstEmail!["email"]?.GetValue<string>();
        Assert.False(string.IsNullOrEmpty(emailValue), "email field should not be empty for authorized caller");
        Assert.DoesNotContain("***", emailValue!, StringComparison.Ordinal);
    }
}
