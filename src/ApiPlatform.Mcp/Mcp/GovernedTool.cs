namespace ApiPlatform.Mcp.Mcp;

/// <summary>
/// Internal descriptor for a registered governed tool.
/// Binds a name, a required scope, and an async handler.
/// </summary>
internal sealed class GovernedTool
{
    public string Name          { get; init; } = string.Empty;
    public string RequiredScope { get; init; } = string.Empty;
    public string Description   { get; init; } = string.Empty;

    /// <summary>
    /// Invoked only after the scope gate passes. Returns a PII-masked response object.
    /// </summary>
    public Func<IReadOnlyDictionary<string, object?>, ToolCallContext, Task<object>> Handler { get; init; } = null!;
}
