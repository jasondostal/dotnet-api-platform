using ApiPlatform.Mcp.Mcp;

namespace ApiPlatform.Mcp;

/// <summary>
/// Thin in-process dispatcher binding tool/resource requests to the governed
/// <see cref="PlatformToolset"/> and <see cref="ResourceCatalog"/>.
/// No stdio/socket transport is required for the reference implementation —
/// the governance pipeline is the value, not the wire protocol.
/// </summary>
public sealed class McpServer
{
    private readonly PlatformToolset _toolset;
    private readonly ResourceCatalog _catalog;

    public McpServer(PlatformToolset toolset, ResourceCatalog catalog)
    {
        _toolset = toolset;
        _catalog = catalog;
    }

    // ── Tool surface ──────────────────────────────────────────────────────────

    /// <summary>
    /// Routes a tool call through the governance pipeline (scope check, PII mask, audit).
    /// </summary>
    public Task<ToolResult> CallToolAsync(
        string toolName,
        IReadOnlyDictionary<string, object?> args,
        ToolCallContext ctx)
        => _toolset.CallToolAsync(toolName, args, ctx);

    // ── Resource surface ──────────────────────────────────────────────────────

    /// <summary>Returns all registered MCP resources.</summary>
    public IReadOnlyList<McpResource> ListResources()
        => _catalog.ListResources();

    /// <summary>
    /// Returns the descriptor for <paramref name="uri"/>, or <c>null</c> if unknown.
    /// </summary>
    public McpResourceDescriptor? ReadResource(string uri)
        => _catalog.ReadResource(uri);
}
