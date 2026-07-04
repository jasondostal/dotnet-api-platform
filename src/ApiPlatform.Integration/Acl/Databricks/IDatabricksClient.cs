namespace ApiPlatform.Integration.Acl.Databricks;

/// <summary>Raw analytic row as returned by the Databricks SQL endpoint before mapping to canonical contracts.</summary>
internal record DatabricksInsightRow(string Metric, string? Dimension, decimal Value, DateOnly? AsOf);

/// <summary>
/// Internal client seam for Databricks analytics.
/// Two implementations exist:
/// <list type="bullet">
///   <item><see cref="StubDatabricksClient"/> — in-memory fixture data; this is the default.</item>
///   <item><see cref="DatabricksSqlClient"/> — live HTTP client; present in-tree but unwired unless explicitly configured.</item>
/// </list>
/// </summary>
internal interface IDatabricksClient
{
    /// <summary>
    /// Returns raw insight rows, optionally starting at <paramref name="cursor"/> for pagination.
    /// </summary>
    Task<IReadOnlyList<DatabricksInsightRow>> FetchInsightRowsAsync(
        string? cursor = null, CancellationToken ct = default);
}
