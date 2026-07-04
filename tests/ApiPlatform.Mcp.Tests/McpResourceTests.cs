namespace ApiPlatform.Mcp.Tests;

/// <summary>
/// Verifies ResourceCatalog contract: expected domains are registered and
/// individual resource descriptors are well-formed.
/// </summary>
public class McpResourceTests
{
    private static ResourceCatalog BuildCatalog() => new();

    // ── ListResources ─────────────────────────────────────────────────────────

    [Fact]
    public void ListResources_ReturnsAllExpectedDomains()
    {
        var catalog   = BuildCatalog();
        var resources = catalog.ListResources();

        Assert.NotEmpty(resources);

        var uris = resources.Select(r => r.Uri).ToList();
        Assert.Contains("contract://accounts",     uris);
        Assert.Contains("contract://customers",    uris);
        Assert.Contains("contract://transactions", uris);
    }

    [Fact]
    public void ListResources_EachResourceHasNameAndDescription()
    {
        var catalog = BuildCatalog();

        foreach (var r in catalog.ListResources())
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Name),
                $"Resource '{r.Uri}' is missing a name.");
            Assert.False(string.IsNullOrWhiteSpace(r.Description),
                $"Resource '{r.Uri}' is missing a description.");
        }
    }

    // ── ReadResource ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("contract://accounts",     "Accounts",     "/v1/accounts")]
    [InlineData("contract://customers",    "Customers",    "/v1/customers")]
    [InlineData("contract://transactions", "Transactions", "/v1/accounts/{accountId}/transactions")]
    public void ReadResource_KnownUri_ReturnsCorrectDescriptor(
        string uri, string expectedName, string expectedPath)
    {
        var catalog    = BuildCatalog();
        var descriptor = catalog.ReadResource(uri);

        Assert.NotNull(descriptor);
        Assert.Equal(uri,          descriptor!.Resource.Uri);
        Assert.Equal(expectedName, descriptor!.Resource.Name);
        Assert.Equal(expectedPath, descriptor!.OpenApiPath);
        Assert.False(string.IsNullOrWhiteSpace(descriptor!.SchemaRef));
    }

    [Fact]
    public void ReadResource_UnknownUri_ReturnsNull()
    {
        var catalog    = BuildCatalog();
        var descriptor = catalog.ReadResource("contract://unknown");
        Assert.Null(descriptor);
    }
}
