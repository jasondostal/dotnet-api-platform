using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ApiPlatform.Tests;

/// <summary>
/// Verifies that protected endpoints correctly deny access when the caller presents
/// no scope, or an insufficient scope, via the X-Scopes header.
/// </summary>
public class AuthForbiddenTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AuthForbiddenTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task NoScope_On_ProtectedEndpoint_Returns401Or403()
    {
        // No X-Scopes header — user is authenticated (dev handler always succeeds)
        // but has no scope claims, so the account.read policy is denied.
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/v1/accounts");

        Assert.True(
            (int)response.StatusCode >= 400,
            $"Expected a 4xx status but got {(int)response.StatusCode}");
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task WrongScope_Returns403()
    {
        // customer.read is present but /v1/accounts requires account.read.
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/accounts");
        request.Headers.Add("X-Scopes", "customer.read");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
