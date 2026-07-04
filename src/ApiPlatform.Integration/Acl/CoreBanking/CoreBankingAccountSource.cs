using ApiPlatform.Contracts;
using ApiPlatform.Integration.Acl;

namespace ApiPlatform.Integration.Acl.CoreBanking;

/// <summary>
/// IAccountVendor implementation for Core Banking.
/// Owns DEPOSIT and LOAN accounts. Backed by <see cref="StubCoreBankingClient"/>;
/// swap the stub for a real HTTP/gRPC client without changing any endpoint or routing code.
/// </summary>
internal sealed class CoreBankingAccountSource : IAccountVendor
{
    private readonly StubCoreBankingClient _client;

    /// <summary>Parameterless constructor uses the stub by default (supports direct unit-test instantiation).</summary>
    public CoreBankingAccountSource() : this(new StubCoreBankingClient()) { }

    public CoreBankingAccountSource(StubCoreBankingClient client)
    {
        _client = client;
    }

    public string SourceSystem => "Core Banking";

    public Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken ct = default)
        => Task.FromResult(_client.GetAccounts());

    public Task<IReadOnlyList<Transaction>> GetTransactionsAsync(Guid accountId, CancellationToken ct = default)
        => Task.FromResult(_client.GetTransactions(accountId));
}
