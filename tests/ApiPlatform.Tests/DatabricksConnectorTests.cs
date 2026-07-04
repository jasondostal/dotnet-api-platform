using ApiPlatform.Integration.Acl;
using ApiPlatform.Integration.Acl.Databricks;
using ApiPlatform.Integration.Runtime;
using ApiPlatform.Platform.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Tests;

/// <summary>
/// Unit-level tests for the Databricks insights connector.
/// Verifies that:
///   1. AddIntegration auto-discovers DatabricksConnectorModule with zero core edits.
///   2. ListInsightsAsync returns fixture rows from the stub.
///   3. Stub is the default — no exception, no live client activated — even without any Databricks config.
///   4. DatabricksSqlClient throws a clear diagnostic when constructed without a connection string.
/// No web host is required; runs from a plain ServiceCollection.
/// </summary>
public class DatabricksConnectorTests
{
    // ── Test helper ───────────────────────────────────────────────────────────

    private static ServiceProvider BuildProvider(Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            // AUDIT_LOG_DIR has a sensible fallback in PlatformAudit.Configure;
            // provide an isolated dir so parallel test runs don't collide.
            ["AUDIT_LOG_DIR"] = Path.Combine(
                Path.GetTempPath(),
                $"audit-databricks-{Guid.NewGuid():N}"),

            // No Databricks:Mode or Databricks:ConnectionString — exercising the default stub path.
        };

        if (overrides is not null)
            foreach (var (k, v) in overrides)
                values[k] = v;

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var services = new ServiceCollection();
        services.AddPlatformCore(config);
        services.AddIntegration(config);   // auto-discovers DatabricksConnectorModule via reflection

        return services.BuildServiceProvider();
    }

    // ── 1. Module is auto-discovered — IInsightSource resolves ───────────────

    [Fact]
    public void AddIntegration_AutoDiscoversDatabricksModule_IInsightSourceResolves()
    {
        using var provider = BuildProvider();

        // If DatabricksConnectorModule were not discovered, this would return null.
        var source = provider.GetService<IInsightSource>();
        Assert.NotNull(source);
    }

    // ── 2. Stub returns expected fixture insight rows ─────────────────────────

    [Fact]
    public async Task ListInsights_DefaultConfig_ReturnsStubRows()
    {
        await using var provider = BuildProvider();

        var source = provider.GetRequiredService<IInsightSource>();
        var list   = await source.ListInsightsAsync();

        Assert.NotEmpty(list.Data);

        // Verify the well-known stub metrics are present.
        var metrics = list.Data.Select(i => i.Metric).ToHashSet();
        Assert.Contains("accounts.opened",   metrics);
        Assert.Contains("balance.avg",       metrics);
        Assert.Contains("loan.originations", metrics);

        // All rows must have a non-negative value and a valid AsOf date.
        foreach (var insight in list.Data)
        {
            Assert.False(string.IsNullOrWhiteSpace(insight.Metric), "Metric must not be empty");
            Assert.True(insight.Value >= 0m, $"Value for {insight.Metric} should be non-negative");
            Assert.NotNull(insight.AsOf);
        }
    }

    // ── 3. Live path is NOT active by default — stub responds without exception ─

    [Fact]
    public async Task ListInsights_NoLiveConfig_StubResponds_NoException()
    {
        // No Databricks:Mode=Live and no Databricks:ConnectionString in config.
        // The connector must return stub data without throwing, proving
        // DatabricksSqlClient is never activated in the default configuration.
        await using var provider = BuildProvider();

        var source = provider.GetRequiredService<IInsightSource>();

        // Must not throw — stub returns fixture rows, live client is not wired.
        var list = await source.ListInsightsAsync();

        Assert.NotNull(list);
        Assert.NotEmpty(list.Data);

        // Sanity: a live client would not have returned the specific stub dimension "retail".
        var retailRows = list.Data.Where(i => i.Dimension == "retail").ToList();
        Assert.NotEmpty(retailRows);
    }

    // ── 4. DatabricksSqlClient without a connection string throws a clear diagnostic ─

    [Fact]
    public void DatabricksSqlClient_NoConnectionString_ThrowsInvalidOperationException()
    {
        // Constructing the live client without configuration must fail loudly at
        // construction time (not buried in a later call), directing the developer
        // back to the stub default.
        var ex = Assert.Throws<InvalidOperationException>(
            () => new DatabricksSqlClient(connectionString: null));

        Assert.Contains("Databricks:ConnectionString", ex.Message, StringComparison.Ordinal);
        Assert.Contains("stub", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
