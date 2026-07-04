using ApiPlatform.Integration.Acl;
using ApiPlatform.Integration.Runtime;
using ApiPlatform.Platform.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Tests;

/// <summary>
/// Unit-level tests for the ClickUp connector.
/// Verifies that:
///   1. AddIntegration auto-discovers ClickUpConnectorModule with zero core edits.
///   2. ListWorkItemsAsync returns items from the stub.
///   3. AssigneeEmail is masked at the ACL boundary — raw vendor addresses never escape.
/// No web host is required; runs from a plain ServiceCollection.
/// </summary>
public class ClickUpConnectorTests
{
    // ── Test helper ───────────────────────────────────────────────────────────

    private static ServiceProvider BuildProvider()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // AUDIT_LOG_DIR has a sensible fallback in PlatformAudit.Configure;
                // provide an isolated dir so parallel test runs don't collide.
                ["AUDIT_LOG_DIR"] = Path.Combine(
                    Path.GetTempPath(),
                    $"audit-clickup-{Guid.NewGuid():N}"),
                ["ClickUp:Mode"] = "Stub",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddPlatformCore(config);
        services.AddIntegration(config);   // auto-discovers ClickUpConnectorModule via reflection

        return services.BuildServiceProvider();
    }

    // ── 1. Module is auto-discovered — IWorkItemSource resolves ──────────────

    [Fact]
    public void AddIntegration_AutoDiscoversClickUpModule_IWorkItemSourceResolves()
    {
        using var provider = BuildProvider();

        // If ClickUpConnectorModule were not discovered, this would throw.
        var source = provider.GetService<IWorkItemSource>();
        Assert.NotNull(source);
    }

    // ── 2. Stub returns at least one work item ────────────────────────────────

    [Fact]
    public async Task ListWorkItems_ReturnsItems()
    {
        await using var provider = BuildProvider();

        var source = provider.GetRequiredService<IWorkItemSource>();
        var list   = await source.ListWorkItemsAsync();

        Assert.NotEmpty(list.Data);
    }

    // ── 3. AssigneeEmail is masked — raw vendor address never escapes ─────────

    [Fact]
    public async Task ListWorkItems_AssigneeEmailIsMasked_RawAddressAbsent()
    {
        await using var provider = BuildProvider();

        var source = provider.GetRequiredService<IWorkItemSource>();
        var list   = await source.ListWorkItemsAsync();

        // The stub seeds two items with assignee emails; at least one must be present.
        var assignedItems = list.Data.Where(w => w.AssigneeEmail is not null).ToList();
        Assert.NotEmpty(assignedItems);

        foreach (var item in assignedItems)
        {
            // Masked form must contain the sentinel — e.g. "j***@e***.com"
            Assert.Contains("***", item.AssigneeEmail!, StringComparison.Ordinal);

            // Raw fixture addresses must NOT appear anywhere in the masked value.
            Assert.DoesNotContain("jane.doe@example.com",       item.AssigneeEmail!, StringComparison.Ordinal);
            Assert.DoesNotContain("alex.smith@clickup-test.io", item.AssigneeEmail!, StringComparison.Ordinal);
        }
    }

    // ── 4. GetWorkItemAsync round-trips via the same masking path ─────────────

    [Fact]
    public async Task GetWorkItem_KnownId_ReturnsMaskedEmail()
    {
        await using var provider = BuildProvider();

        var source = provider.GetRequiredService<IWorkItemSource>();
        var list   = await source.ListWorkItemsAsync();

        // Pick the first item that has an assignee so we can verify the get path too.
        var target = list.Data.First(w => w.AssigneeEmail is not null);

        var fetched = await source.GetWorkItemAsync(target.WorkItemId);

        Assert.NotNull(fetched);
        Assert.Equal(target.WorkItemId, fetched!.WorkItemId);
        Assert.Contains("***", fetched.AssigneeEmail!, StringComparison.Ordinal);
        Assert.DoesNotContain("jane.doe@example.com", fetched.AssigneeEmail!, StringComparison.Ordinal);
    }
}
