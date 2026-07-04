using ApiPlatform.Contracts;
using ApiPlatform.Integration.Acl;

namespace ApiPlatform.Mcp.Tests.Stubs;

/// <summary>
/// In-memory stub that returns a single seeded account with a known account-number display,
/// allowing tests to assert that PII masking is applied before results leave the toolset.
/// </summary>
internal sealed class StubAccountSource : IAccountSource
{
    /// <summary>The raw account number display value — must NOT appear in toolset output.</summary>
    public const string RawAccountNumberDisplay = "9876543210";

    public Task<AccountList> ListAccountsAsync(string? cursor, int limit, CancellationToken ct = default)
        => Task.FromResult(new AccountList
        {
            Data =
            [
                new Account
                {
                    AccountId            = new Guid("a1000000-0000-0000-0000-000000000001"),
                    AccountType          = AccountType.DEPOSIT,
                    AccountNumberDisplay = RawAccountNumberDisplay,
                    Nickname             = "Primary Checking",
                    Status               = AccountStatus.OPEN,
                    Currency             = "USD",
                    ProductName          = "Everyday Checking"
                }
            ]
        });

    public Task<Account?> GetAccountAsync(Guid accountId, CancellationToken ct = default)
        => Task.FromResult<Account?>(null);

    public Task<TransactionList> ListTransactionsAsync(
        Guid accountId, string? cursor, int limit, CancellationToken ct = default)
        => Task.FromResult(new TransactionList());
}
