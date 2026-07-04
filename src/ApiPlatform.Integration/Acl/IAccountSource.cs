using ApiPlatform.Contracts;
using ApiPlatform.Platform.Connectors;

namespace ApiPlatform.Integration.Acl;

/// <summary>
/// Anti-corruption layer interface abstracting account source systems.
/// Implementations may be swapped without affecting endpoint logic.
/// </summary>
public interface IAccountSource : IGovernedSource
{
    Task<AccountList> ListAccountsAsync(string? cursor, int limit, CancellationToken ct = default);
    Task<Account?> GetAccountAsync(Guid accountId, CancellationToken ct = default);
    Task<TransactionList> ListTransactionsAsync(Guid accountId, string? cursor, int limit, CancellationToken ct = default);
}
