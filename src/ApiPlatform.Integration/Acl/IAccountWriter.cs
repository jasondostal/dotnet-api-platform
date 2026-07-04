using ApiPlatform.Contracts;
using ApiPlatform.Platform.Connectors;

namespace ApiPlatform.Integration.Acl;

/// <summary>
/// Write seam for account-bearing source systems (e.g. the core banking platform): create an
/// account/member, change values, renew. Extends <see cref="IGovernedSource"/> so every write
/// is audited + traced by construction, with no audit code in the adapter. This is the shape
/// every one of the ~70 vendor integrations will follow.
/// </summary>
public interface IAccountWriter : IGovernedSource
{
    Task<Account> CreateAccountAsync(Account draft, CancellationToken ct = default);

    Task<Account?> ChangeAccountStatusAsync(Guid accountId, AccountStatus status, CancellationToken ct = default);
}
