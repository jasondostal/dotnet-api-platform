using ApiPlatform.Platform.Errors;

namespace ApiPlatform.Mcp.Mcp;

/// <summary>
/// The outcome of a governed tool invocation. On denial <see cref="Problem"/> is set;
/// on success <see cref="Content"/> holds the PII-masked response object.
/// </summary>
public sealed class ToolResult
{
    public bool IsSuccess { get; init; }

    /// <summary>PII-masked response content (non-null when <see cref="IsSuccess"/> is true).</summary>
    public object? Content { get; init; }

    /// <summary>Problem type on denial (non-null when <see cref="IsSuccess"/> is false).</summary>
    public ProblemType? Problem { get; init; }

    /// <summary>Human-readable detail accompanying <see cref="Problem"/>.</summary>
    public string? ProblemDetail { get; init; }

    public static ToolResult Success(object content)
        => new() { IsSuccess = true, Content = content };

    public static ToolResult Denied(ProblemType problem, string detail)
        => new() { IsSuccess = false, Problem = problem, ProblemDetail = detail };
}
