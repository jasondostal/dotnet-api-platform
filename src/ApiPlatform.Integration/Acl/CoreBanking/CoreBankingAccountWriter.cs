namespace ApiPlatform.Integration.Acl.CoreBanking;

/// <summary>
/// Stub core-banking write adapter — create an account, change its status. A live adapter calls
/// the core's write API; this returns deterministic results so the repo runs offline. Note there
/// is NO audit or tracing code here: the governance proxy audits every write automatically.
/// </summary>
internal sealed class CoreBankingAccountWriter : IAccountWriter
{
    public Task<Account> CreateAccountAsync(Account draft, CancellationToken ct = default)
    {
        // A real adapter posts to the core; the stub assigns an id and echoes the draft back.
        draft.AccountId = Guid.NewGuid();
        draft.Status = AccountStatus.OPEN;
        return Task.FromResult(draft);
    }

    public Task<Account?> ChangeAccountStatusAsync(Guid accountId, AccountStatus status, CancellationToken ct = default)
    {
        var account = new Account { AccountId = accountId, Status = status };
        return Task.FromResult<Account?>(account);
    }
}
