using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiPlatform.Tests;

/// <summary>
/// Verifies the idempotency middleware replays stored responses for repeated unsafe
/// requests that carry the same Idempotency-Key header.
/// </summary>
public class IdempotencyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    // Seeded deposit account from CoreBankingAccountSource
    private const string DepositId = "f47ac10b-58cc-4372-a567-0e02b2c3d479";

    public IdempotencyTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TouchAccount_Twice_WithSameKey_SecondResponseHasReplayedHeader()
    {
        var client = _factory.CreateClient();
        var idempotencyKey = Guid.NewGuid().ToString();

        // ── First request ─────────────────────────────────────────────────────
        var req1 = new HttpRequestMessage(HttpMethod.Post, $"/v1/accounts/{DepositId}/touch");
        req1.Headers.Add("X-Scopes", "event.publish");
        req1.Headers.Add("Idempotency-Key", idempotencyKey);

        var res1 = await client.SendAsync(req1);

        Assert.True(res1.IsSuccessStatusCode, $"First request failed with {(int)res1.StatusCode}");
        Assert.False(
            res1.Headers.Contains("Idempotency-Replayed"),
            "First response must NOT carry Idempotency-Replayed");

        // ── Second request — identical key ────────────────────────────────────
        var req2 = new HttpRequestMessage(HttpMethod.Post, $"/v1/accounts/{DepositId}/touch");
        req2.Headers.Add("X-Scopes", "event.publish");
        req2.Headers.Add("Idempotency-Key", idempotencyKey);

        var res2 = await client.SendAsync(req2);

        Assert.True(res2.IsSuccessStatusCode, $"Replayed request failed with {(int)res2.StatusCode}");
        Assert.True(
            res2.Headers.Contains("Idempotency-Replayed"),
            "Second response must carry Idempotency-Replayed header");
        Assert.Equal(
            "true",
            res2.Headers.GetValues("Idempotency-Replayed").FirstOrDefault());
    }
}
