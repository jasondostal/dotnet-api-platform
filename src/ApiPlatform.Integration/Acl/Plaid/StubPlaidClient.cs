namespace ApiPlatform.Integration.Acl.Plaid;

/// <summary>
/// In-memory stub for the Plaid Items API.
/// Returns seed account and balance data in Plaid-native shape.
/// Swap for a real Plaid HTTP client (using your Items access token) without
/// changing any source, routing, or endpoint code.
/// Only consulted when <c>Plaid:Enabled=true</c>.
/// </summary>
internal sealed class StubPlaidClient
{
    // ── Vendor-native DTOs (Plaid-style snake_case — never exposed beyond this folder) ──

    internal sealed record PlaidAccountRaw(
        string account_id,      // Plaid's opaque account identifier
        string mask,            // last-four display mask, e.g. "7890"
        string name,            // institution short name
        string official_name,   // full product name
        string type,            // "depository" | "credit" | "loan" | "investment"
        string subtype,         // "checking" | "savings" | "credit card" | …
        decimal current,        // balances.current
        decimal? available      // balances.available (null when not applicable)
    );

    // ── Seed data ─────────────────────────────────────────────────────────────

    private static readonly IReadOnlyList<PlaidAccountRaw> SeedAccounts =
    [
        new PlaidAccountRaw(
            account_id    : "plaid-acct-0001",
            mask          : "7890",
            name          : "Plaid Checking",
            official_name : "ApiPlatform Platinum Checking",
            type          : "depository",
            subtype       : "checking",
            current       :  3_200.00m,
            available     :  3_150.00m
        ),
        new PlaidAccountRaw(
            account_id    : "plaid-acct-0002",
            mask          : "4321",
            name          : "Plaid Savings",
            official_name : "ApiPlatform High-Yield Savings",
            type          : "depository",
            subtype       : "savings",
            current       : 12_500.00m,
            available     : 12_500.00m
        ),
    ];

    internal IReadOnlyList<PlaidAccountRaw> GetAccounts() => SeedAccounts;
}
