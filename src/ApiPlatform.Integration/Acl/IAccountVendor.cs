using ApiPlatform.Contracts;

namespace ApiPlatform.Integration.Acl;

/// <summary>
/// Contract for a single vendor source system that contributes accounts to the canonical layer.
/// Each vendor owns a subset of account types and translates its raw shape to canonical models.
/// </summary>
public interface IAccountVendor
{
    string SourceSystem { get; }
    Task<IReadOnlyList<Account>> GetAccountsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetTransactionsAsync(Guid accountId, CancellationToken ct = default);
}
