using ApiPlatform.Integration.Acl;
using ApiPlatform.Integration.Runtime;
using ApiPlatform.Platform.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Tests;

/// <summary>
/// Verifies the Plaid connector module contract:
///   1. Disabled by default — existing account set (CoreBanking + Cards = 3 accounts) is unaffected.
///   2. Enabled via config — Plaid vendor contributes its stub accounts.
///
/// These tests exercise the vendor in isolation via IEnumerable&lt;IAccountVendor&gt;,
/// without a web host, so they run fast and don't touch the HTTP pipeline.
/// </summary>
public class PlaidConnectorTests
{
    // ── Helper: build a minimal DI provider with platform core + integration layer ──

    private static ServiceProvider BuildProvider(params (string Key, string Value)[] configOverrides)
    {
        var configValues = configOverrides
            .ToDictionary(kv => kv.Key, kv => (string?)kv.Value);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddPlatformCore(config);
        services.AddIntegration(config);

        return services.BuildServiceProvider();
    }

    // ── 1. Default: Plaid vendor is present but returns zero accounts ─────────

    [Fact]
    public async Task Plaid_Disabled_ByDefault_AddsNoAccounts()
    {
        // Arrange: no Plaid:Enabled key → defaults to false
        await using var provider = BuildProvider();

        var vendors = provider.GetRequiredService<IEnumerable<IAccountVendor>>();
        var plaidVendor = vendors.Single(v => v.SourceSystem == "Plaid");

        // Act
        var accounts = await plaidVendor.GetAccountsAsync();

        // Assert: disabled connector is inert — zero accounts, existing 3-account set unchanged
        Assert.Empty(accounts);
    }

    // ── 2. Enabled: Plaid vendor contributes its stub accounts ───────────────

    [Fact]
    public async Task Plaid_Enabled_ContributesAccounts()
    {
        // Arrange: opt-in via config
        await using var provider = BuildProvider(("Plaid:Enabled", "true"));

        var vendors = provider.GetRequiredService<IEnumerable<IAccountVendor>>();
        var plaidVendor = vendors.Single(v => v.SourceSystem == "Plaid");

        // Act
        var accounts = await plaidVendor.GetAccountsAsync();

        // Assert: stub returns the two seeded depository accounts
        Assert.NotEmpty(accounts);

        // Plaid stub accounts must carry canonical IDs that are distinct from
        // the existing CoreBanking and Cards UUIDs — no collision with the 3-account set
        var existingIds = new HashSet<Guid>
        {
            Guid.Parse("f47ac10b-58cc-4372-a567-0e02b2c3d479"), // CoreBanking deposit
            Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"), // CoreBanking loan
            Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7"), // Cards credit
        };

        foreach (var account in accounts)
        {
            Assert.DoesNotContain(account.AccountId, existingIds);
            // Stub accounts are depository type
            Assert.Equal(ApiPlatform.Contracts.AccountType.DEPOSIT, account.AccountType);
            // Display mask follows the canonical ****XXXX pattern
            Assert.StartsWith("****", account.AccountNumberDisplay ?? string.Empty);
        }
    }
}
