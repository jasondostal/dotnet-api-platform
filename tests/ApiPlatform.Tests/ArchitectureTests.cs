using System.Reflection;
using NetArchTest.Rules;

namespace ApiPlatform.Tests;

/// <summary>
/// Architectural enforcement tests via NetArchTest.Rules.
///
/// Assembly anchors (resolved at runtime, not via ProjectReference strings):
///   Contracts   → typeof(ApiPlatform.Contracts.Account).Assembly
///   Platform    → typeof(ApiPlatform.Platform.Pii.IPiiRedactor).Assembly
///   Integration → typeof(ApiPlatform.Integration.Acl.IAccountSource).Assembly
/// </summary>
public class ArchitectureTests
{
    private static readonly Assembly _contracts =
        typeof(ApiPlatform.Contracts.Account).Assembly;

    private static readonly Assembly _platform =
        typeof(ApiPlatform.Platform.Pii.IPiiRedactor).Assembly;

    private static readonly Assembly _integration =
        typeof(ApiPlatform.Integration.Acl.IAccountSource).Assembly;

    // ── Rule 1: ApiPlatform.Platform core has no ASP.NET Core dependency ──────
    // Platform is the governance core (audit, auth, PII, runtime). It must not
    // pull in the web layer so it can be used in off-path jobs and CLI tools.

    [Fact]
    public void PlatformCore_HasNo_AspNetCore_Dependency()
    {
        var result = Types.InAssembly(_platform)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Platform core must not reference Microsoft.AspNetCore. " +
            $"Failing: {Failing(result)}");
    }

    // ── Rule 2: ApiPlatform.Contracts has no web, cloud, or integration deps ──
    // Contracts are pure model types. Nothing above them in the dependency graph
    // should leak into this assembly.

    [Fact]
    public void Contracts_HasNo_AspNetCore_Dependency()
    {
        var result = Types.InAssembly(_contracts)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Contracts must not reference Microsoft.AspNetCore. " +
            $"Failing: {Failing(result)}");
    }

    [Fact]
    public void Contracts_HasNo_Azure_Dependency()
    {
        var result = Types.InAssembly(_contracts)
            .ShouldNot()
            .HaveDependencyOn("Azure")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Contracts must not reference Azure.* assemblies. " +
            $"Failing: {Failing(result)}");
    }

    [Fact]
    public void Contracts_HasNo_Integration_Dependency()
    {
        var result = Types.InAssembly(_contracts)
            .ShouldNot()
            .HaveDependencyOn("ApiPlatform.Integration")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Contracts must not reference ApiPlatform.Integration. " +
            $"Failing: {Failing(result)}");
    }

    // ── Rule 3: ApiPlatform.Integration has no web or Api layer dependency ────
    // Integration connects to external vendor systems. It must not depend on the
    // HTTP host (that would invert the layer diagram) or on ApiPlatform.Api
    // (which would create a cycle through the composition root).

    [Fact]
    public void Integration_HasNo_AspNetCore_Dependency()
    {
        var result = Types.InAssembly(_integration)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Integration must not reference Microsoft.AspNetCore. " +
            $"Failing: {Failing(result)}");
    }

    [Fact]
    public void Integration_HasNo_ApiPlatformApi_Dependency()
    {
        var result = Types.InAssembly(_integration)
            .ShouldNot()
            .HaveDependencyOn("ApiPlatform.Api")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Integration must not reference ApiPlatform.Api. " +
            $"Failing: {Failing(result)}");
    }

    // ── Rule 4: Vendor ACL *Source adapter classes must be internal ───────────
    // Source adapters are the private translation layer between external vendor APIs
    // and the platform model. They must never leak as public types — the only public
    // surface of the ACL is the canonical I{Domain}Source interfaces. One rule covers
    // every vendor folder (CoreBanking, Cards, ClickUp, Plaid, Databricks) and the
    // routing aggregator; a future *Source accidentally made public fails the build.

    [Fact]
    public void VendorSourceAdapterClasses_AreInternal()
    {
        var result = Types.InAssembly(_integration)
            .That()
            .AreClasses()
            .And()
            .ResideInNamespace("ApiPlatform.Integration.Acl")
            .And()
            .HaveNameMatching(".*(Source|Writer)$")
            .ShouldNot()
            .BePublic()
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Vendor *Source adapter classes must be internal (only the I*Source interfaces are public). " +
            $"Failing: {Failing(result)}");
    }

    // ── Rule 5: Connector modules must be public (backstops analyzer APL0002) ──
    // IConnectorModule implementations are discovered by assembly scan + Activator;
    // a non-public module is silently never registered. The analyzer catches this at
    // compile time; this test is the architectural backstop.

    [Fact]
    public void ConnectorModules_ArePublic()
    {
        var result = Types.InAssembly(_integration)
            .That()
            .ImplementInterface(typeof(ApiPlatform.Platform.Connectors.IConnectorModule))
            .Should()
            .BePublic()
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Connector modules must be public for registry discovery. " +
            $"Failing: {Failing(result)}");
    }

    // ── Rule 6: Layer direction holds both ways ───────────────────────────────
    // Core never depends upward on Integration; Integration depends on the core,
    // never on the web layer (Platform.AspNetCore). Together with the no-Api and
    // no-AspNetCore rules above, this keeps the seam the only door to vendor data.

    [Fact]
    public void PlatformCore_DoesNotDependOn_Integration()
    {
        var result = Types.InAssembly(_platform)
            .ShouldNot()
            .HaveDependencyOn("ApiPlatform.Integration")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Platform core must not depend upward on Integration. " +
            $"Failing: {Failing(result)}");
    }

    [Fact]
    public void Integration_DoesNotDependOn_PlatformAspNetCore()
    {
        var result = Types.InAssembly(_integration)
            .ShouldNot()
            .HaveDependencyOn("ApiPlatform.Platform.AspNetCore")
            .GetResult();

        Assert.True(result.IsSuccessful,
            "Integration must depend on Platform core only, not the web layer. " +
            $"Failing: {Failing(result)}");
    }

    // ── Rule 7: Canonical seam interfaces implement IGovernedSource ──────────
    // IGovernedSource is the explicit marker that opts a seam interface into
    // automatic audit, tracing, and PII masking. All five canonical seams must
    // carry the marker — a seam without it escapes governance silently.

    [Fact]
    public void CanonicalSeams_ImplementIGovernedSource()
    {
        var governed = typeof(ApiPlatform.Platform.Connectors.IGovernedSource);

        var seamTypes = new[]
        {
            typeof(ApiPlatform.Integration.Acl.IAccountSource),
            typeof(ApiPlatform.Integration.Acl.ICustomerSource),
            typeof(ApiPlatform.Integration.Acl.IWorkItemSource),
            typeof(ApiPlatform.Integration.Acl.IInsightSource),
            typeof(ApiPlatform.Integration.Acl.IAccountWriter),
        };

        var missing = seamTypes
            .Where(t => !governed.IsAssignableFrom(t))
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(missing);
    }

    // ── Helper ────────────────────────────────────────────────────────────────

    private static string Failing(TestResult result) =>
        result.FailingTypes is { Count: > 0 }
            ? string.Join(", ", result.FailingTypes.Select(t => t.FullName))
            : "(none)";
}
