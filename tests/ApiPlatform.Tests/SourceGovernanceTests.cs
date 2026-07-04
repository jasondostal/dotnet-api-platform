using ApiPlatform.Contracts;
using ApiPlatform.Integration.Acl;
using ApiPlatform.Integration.Acl.Governance;
using ApiPlatform.Integration.Runtime;
using ApiPlatform.Platform.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Tests;

/// <summary>
/// Proves audit-by-default is universal and by-construction: EVERY seam interface (read or write,
/// every vendor) resolves to a governance proxy, and every operation through it — including writes
/// like create/change — is audited, with zero audit code in the adapter. A new vendor source is
/// governed the moment it is registered; nobody re-implements this 70 times.
/// </summary>
public class SourceGovernanceTests
{
    private static ServiceProvider Build(string auditDir)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AUDIT_LOG_DIR"] = auditDir })
            .Build();

        return new ServiceCollection()
            .AddPlatformCore(config)
            .AddIntegration(config)
            .BuildServiceProvider();
    }

    [Theory]
    [InlineData(typeof(IAccountSource))]
    [InlineData(typeof(ICustomerSource))]
    [InlineData(typeof(IWorkItemSource))]
    [InlineData(typeof(IInsightSource))]
    [InlineData(typeof(IAccountWriter))]   // a WRITE seam — governed exactly like the read seams
    public void EverySeam_ResolvesToAGovernanceProxy(Type seamType)
    {
        using var sp = Build(Path.Combine(Path.GetTempPath(), $"gov-{Guid.NewGuid():N}"));

        var resolved = sp.GetRequiredService(seamType);

        // DynamicProxy emits proxy types into the Castle.Proxies namespace — proof the seam is wrapped.
        Assert.Equal("Castle.Proxies", resolved.GetType().Namespace);
    }

    [Fact]
    public async Task Read_IsAudited()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"gov-read-{Guid.NewGuid():N}");
        using var sp = Build(dir);

        await sp.GetRequiredService<IAccountSource>().ListAccountsAsync(cursor: null, limit: 50);

        var content = ReadAudit(dir);
        Assert.Contains("ListAccounts", content);   // operation
        Assert.Contains("allowed", content);         // outcome
        Assert.Contains("system", content);          // actor
    }

    [Fact]
    public async Task Write_IsAudited_WithZeroAuditCodeInTheAdapter()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"gov-write-{Guid.NewGuid():N}");
        using var sp = Build(dir);

        // The CoreBanking write adapter contains NO audit code — the governance proxy audits it.
        await sp.GetRequiredService<IAccountWriter>()
            .CreateAccountAsync(new Account { AccountType = AccountType.DEPOSIT });

        var content = ReadAudit(dir);
        Assert.Contains("CreateAccount", content);   // the write operation is captured
        Assert.Contains("allowed", content);
        Assert.Contains("system", content);
    }

    // ── Namespace-escape hole is closed ──────────────────────────────────────
    // A seam declared in ANY namespace — not only ApiPlatform.Integration.Acl — is still
    // governed if it extends IGovernedSource. This proves the old namespace-string predicate
    // has been replaced by a type-relationship predicate.

    [Fact]
    public void SeamInDifferentNamespace_StillWrappedByProxy_WhenItImplementsIGovernedSource()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"gov-ns-{Guid.NewGuid():N}");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AUDIT_LOG_DIR"] = dir })
            .Build();

        // Register the out-of-namespace seam plus platform core, then call GovernSources.
        var services = new ServiceCollection();
        services.AddPlatformCore(config);
        services.AddSingleton<SomeOther.Place.IWidgetSource>(_ => new SomeOther.Place.WidgetSourceStub());
        services.GovernSources();

        using var sp = services.BuildServiceProvider();
        var resolved = sp.GetRequiredService<SomeOther.Place.IWidgetSource>();

        // DynamicProxy emits proxy types into Castle.Proxies — proof the seam is wrapped
        // even though it lives outside the ApiPlatform.Integration.Acl namespace.
        Assert.Equal("Castle.Proxies", resolved.GetType().Namespace);
    }

    private static string ReadAudit(string dir)
    {
        var files = Directory.Exists(dir) ? Directory.GetFiles(dir) : [];
        Assert.NotEmpty(files);
        return string.Concat(files.Select(File.ReadAllText));
    }
}
