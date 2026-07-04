using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ApiPlatform.Platform.Audit;
using ApiPlatform.Platform.Pii;
using ApiPlatform.Platform.Runtime;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Tests;

/// <summary>
/// Phase 8 — governance Definition of Done (DoD), consolidated.
///
/// Tests cover:
///   1. Scope-gated PII projection — contact absent without contact.read
///   2. Auth enforcement — no scope yields 401/403 on a protected endpoint
///   3. Off-path platform core — AddPlatformCore audits and masks without a web host
///   4. Drift readiness — /health and /openapi/v1.json are always mapped
///
/// Tests 1, 2, and 4 use WebApplicationFactory&lt;Program&gt;. Test 3 uses a plain DI
/// container mirroring PlatformCoreRuntimeTests.
/// </summary>
public class GoldenPathGovernanceTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    // Avery Lindgren — seed customer in StubCoreBankingClient
    private const string AveryId = "3f1a7c20-9b54-4e11-a8d3-1c2b3a4d5e6f";

    public GoldenPathGovernanceTests(WebApplicationFactory<Program> factory)
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

    // ── 1. PII projection: contact absent when contact.read scope is missing ──

    [Fact]
    public async Task MaskedPii_OverHttp_WithoutContactScope_ContactAbsent()
    {
        var client = CreateClient("customer.read"); // no contact.read
        var response = await client.GetAsync($"/v1/customers/{AveryId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonObject>();
        Assert.NotNull(body);

        // Identity fields present
        Assert.Equal(AveryId, body!["customerId"]?.GetValue<string>());

        // contact must be absent — no PII on the wire without explicit scope
        Assert.False(
            body!.AsObject().ContainsKey("contact"),
            "contact must be absent when only customer.read scope is present; " +
            "the endpoint must strip PII before serialization");
    }

    // ── 2. Auth enforcement: no scope → 401 or 403 on protected endpoint ─────
    //
    // ScopeHeaderAuthHandler always authenticates (sets ClaimsPrincipal) so the
    // response will typically be 403 (authorized user, no scope claim). Either
    // 401 or 403 satisfies this rule.

    [Fact]
    public async Task NoScope_OnProtectedEndpoint_IsForbidden()
    {
        var client = CreateClient(); // no X-Scopes header
        var response = await client.GetAsync("/v1/accounts");

        Assert.True(
            response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            $"Expected 401 or 403 on a scoped endpoint with no credentials; " +
            $"got {(int)response.StatusCode} {response.StatusCode}");
    }

    // ── 3. Off-path: AddPlatformCore resolves audit + PII without a web host ──

    [Fact]
    public async Task OffPath_AddPlatformCore_AuditsAndMasks()
    {
        var auditDir = Path.Combine(
            Path.GetTempPath(),
            $"gov-audit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(auditDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AUDIT_LOG_DIR"] = auditDir
            })
            .Build();

        var services = new ServiceCollection();
        services.AddPlatformCore(config);
        await using var provider = services.BuildServiceProvider();

        // Verify PII masking
        var redactor = provider.GetRequiredService<IPiiRedactor>();
        var masked   = redactor.MaskEmail("avery.lindgren@example.com");
        Assert.Contains("***", masked);
        Assert.DoesNotContain("avery.lindgren", masked, StringComparison.OrdinalIgnoreCase);

        // Verify audit write
        var audit = provider.GetRequiredService<IPlatformAudit>();
        await audit.RecordAsync("Governance:DoD", new { masked, run = "phase8" });

        var files = Directory.GetFiles(auditDir, "audit-*.json");
        Assert.NotEmpty(files);
    }

    // ── 4. Drift readiness: /health and /openapi/v1.json are always mapped ───
    //
    // MapOpenApi() runs unconditionally in Program.cs (not gated on IsDevelopment)
    // so drift-detection scripts can always reach /openapi/v1.json.

    [Fact]
    public async Task OpenApiAndHealth_AreMapped()
    {
        var client = _factory.CreateClient();

        var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        var openapi = await client.GetAsync("/openapi/v1.json");
        Assert.Equal(HttpStatusCode.OK, openapi.StatusCode);
    }
}
