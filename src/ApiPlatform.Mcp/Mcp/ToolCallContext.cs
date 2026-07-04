namespace ApiPlatform.Mcp.Mcp;

/// <summary>
/// Carries the governance context for a single tool invocation:
/// the caller's granted scopes and their identity.
/// </summary>
public sealed class ToolCallContext
{
    /// <summary>Scopes that were granted to the calling agent token.</summary>
    public IReadOnlyList<string> GrantedScopes { get; init; } = [];

    /// <summary>Opaque caller identifier used in audit records.</summary>
    public string CallerId { get; init; } = "anonymous";

    public CancellationToken CancellationToken { get; init; } = default;
}
