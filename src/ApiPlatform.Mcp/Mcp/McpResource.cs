namespace ApiPlatform.Mcp.Mcp;

/// <summary>
/// MCP-style resource descriptor: a named, addressable contract domain.
/// </summary>
public sealed class McpResource
{
    public string Uri  { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

/// <summary>
/// Extended descriptor returned by <see cref="ResourceCatalog.ReadResource"/>.
/// Includes an offline reference to the corresponding OpenAPI path and schema.
/// </summary>
public sealed class McpResourceDescriptor
{
    public McpResource Resource   { get; init; } = new();
    public string? OpenApiPath    { get; init; }
    public string? SchemaRef      { get; init; }
}
