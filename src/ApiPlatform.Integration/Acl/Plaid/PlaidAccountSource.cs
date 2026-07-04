using ApiPlatform.Contracts;

namespace ApiPlatform.Integration.Acl.Plaid;

/// <summary>
/// IAccountVendor implementation for Plaid.
/// <para>
/// <b>Disabled by default.</b> Set <c>Plaid:Enabled=true</c> in configuration to activate.
/// When disabled the vendor is still registered and visible to the routing aggregator
/// but returns empty account and transaction collections, leaving the existing account
/// set completely unaffected.
/// </para>
/// <para>
/// When enabled, reads from <see cref="StubPlaidClient"/> and maps Plaid-native DTOs to
/// the canonical <see cref="Account"/> model. Swap the stub for a real Plaid HTTP client
/// without touching any endpoint or aggregation code.
/// </para>
/// </summary>
internal sealed class PlaidAccountSource : IAccountVendor
{
    private readonly StubPlaidClient _client;
    private readonly bool _enabled;

    // Plaid account_id → opaque canonical UUID.
    // Plaid's account_id never escapes this class.
    private static readonly Dictionary<string, Guid> PlaidIdToCanonicalId = new()
    {
        ["plaid-acct-0001"] = Guid.Parse("b1c2d3e4-f5a6-7890-bcde-f01234567890"),
        ["plaid-acct-0002"] = Guid.Parse("c2d3e4f5-a6b7-8901-cdef-012345678901"),
    };

    public PlaidAccountSource(StubPlaidClient client, bool enabled)
    {
        _client  = client;
        _enabled = enabled;
    }

    public string SourceSystem => "Plaid";

    public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken ct = default)
    {
        if (!_enabled)
            return Task.FromResult<IReadOnlyList<Account>>(Array.Empty<Account>());

        IReadOnlyList<Account> result = _client.GetAccounts().Select(MapAccount).ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Transaction>> GetTransactionsAsync(Guid accountId, CancellationToken ct = default)
    {
        // Plaid transaction sync is out of scope for this phase; return empty until implemented.
        return Task.FromResult<IReadOnlyList<Transaction>>(Array.Empty<Transaction>());
    }

    // ── Raw → canonical mapping ───────────────────────────────────────────────

    private static Account MapAccount(StubPlaidClient.PlaidAccountRaw raw)
    {
        if (!PlaidIdToCanonicalId.TryGetValue(raw.account_id, out var canonicalId))
            canonicalId = Guid.NewGuid(); // safety net for unmapped future accounts

        var accountType = raw.type switch
        {
            "credit" => AccountType.CREDIT,
            "loan"   => AccountType.LOAN,
            _        => AccountType.DEPOSIT, // "depository" and any unknown type
        };

        return new Account
        {
            AccountId            = canonicalId,
            AccountType          = accountType,
            AccountNumberDisplay = $"****{raw.mask}",
            Nickname             = raw.name,
            Status               = AccountStatus.OPEN,
            Currency             = "USD",
            ProductName          = raw.official_name,
            DepositAccount       = accountType == AccountType.DEPOSIT
                ? new DepositDetail
                {
                    CurrentBalance   = raw.current,
                    AvailableBalance = raw.available ?? raw.current,
                }
                : null,
        };
    }

    private static AccountStatus MapStatus(string plaidStatus) => plaidStatus switch
    {
        "closed"  => AccountStatus.CLOSED,
        "frozen"  => AccountStatus.FROZEN,
        _         => AccountStatus.OPEN,
    };
}
