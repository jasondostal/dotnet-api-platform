using ApiPlatform.Mcp.Mcp;

namespace ApiPlatform.Mcp;

/// <summary>
/// Exposes the platform contract as MCP-style resources — one per domain.
/// Resources are offline descriptors; no network call is made.
/// </summary>
public sealed class ResourceCatalog
{
    private static readonly IReadOnlyList<McpResource> KnownResources =
    [
        new()
        {
            Uri         = "contract://accounts",
            Name        = "Accounts",
            Description = "Financial accounts (deposit, credit, loan) belonging to customers."
        },
        new()
        {
            Uri         = "contract://customers",
            Name        = "Customers",
            Description = "Customer identity and contact records."
        },
        new()
        {
            Uri         = "contract://transactions",
            Name        = "Transactions",
            Description = "Ledger transactions posted against accounts."
        }
    ];

    private static readonly IReadOnlyDictionary<string, McpResourceDescriptor> Descriptors =
        new Dictionary<string, McpResourceDescriptor>(StringComparer.Ordinal)
        {
            ["contract://accounts"] = new()
            {
                Resource    = KnownResources[0],
                OpenApiPath = "/v1/accounts",
                SchemaRef   = "#/components/schemas/AccountList"
            },
            ["contract://customers"] = new()
            {
                Resource    = KnownResources[1],
                OpenApiPath = "/v1/customers",
                SchemaRef   = "#/components/schemas/CustomerList"
            },
            ["contract://transactions"] = new()
            {
                Resource    = KnownResources[2],
                OpenApiPath = "/v1/accounts/{accountId}/transactions",
                SchemaRef   = "#/components/schemas/TransactionList"
            }
        };

    /// <summary>Returns all registered MCP resources.</summary>
    public IReadOnlyList<McpResource> ListResources() => KnownResources;

    /// <summary>
    /// Returns the extended descriptor for <paramref name="uri"/>,
    /// or <c>null</c> if no resource is registered at that URI.
    /// </summary>
    public McpResourceDescriptor? ReadResource(string uri)
        => Descriptors.TryGetValue(uri, out var d) ? d : null;
}
