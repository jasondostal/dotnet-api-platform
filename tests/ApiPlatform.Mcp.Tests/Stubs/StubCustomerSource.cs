using ApiPlatform.Contracts;
using ApiPlatform.Integration.Acl;

namespace ApiPlatform.Mcp.Tests.Stubs;

/// <summary>Minimal customer source stub — returns empty for all queries.</summary>
internal sealed class StubCustomerSource : ICustomerSource
{
    public Task<CustomerList> ListCustomersAsync(string? cursor, int limit, CancellationToken ct = default)
        => Task.FromResult(new CustomerList());

    public Task<Customer?> GetCustomerAsync(Guid customerId, CancellationToken ct = default)
        => Task.FromResult<Customer?>(null);
}
