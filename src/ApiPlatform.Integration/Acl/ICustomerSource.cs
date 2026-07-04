using ApiPlatform.Contracts;
using ApiPlatform.Platform.Connectors;

namespace ApiPlatform.Integration.Acl;

/// <summary>
/// Anti-corruption layer interface abstracting customer source systems.
/// Implementations may be swapped without affecting endpoint logic.
/// </summary>
public interface ICustomerSource : IGovernedSource
{
    Task<CustomerList> ListCustomersAsync(string? cursor, int limit, CancellationToken ct = default);
    Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken ct = default);
}
