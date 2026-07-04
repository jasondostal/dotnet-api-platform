namespace ApiPlatform.Integration.Acl.Databricks;

/// <summary>
/// Live Databricks SQL HTTP client using the Statement Execution API.
///
/// INTENTIONALLY UNWIRED: this class is present in-tree but is never registered by
/// <see cref="DatabricksConnectorModule"/> unless both <c>Databricks:Mode=Live</c> and
/// <c>Databricks:ConnectionString</c> are present in configuration. Cloning and running
/// the project requires no Databricks subscription — the stub client is used by default.
///
/// No SQL driver NuGet package is referenced. The intended implementation uses Databricks'
/// HTTP Statement Execution API (POST /api/2.0/sql/statements). Add an HttpClient and fill
/// in the call body when wiring for a real environment.
/// </summary>
internal sealed class DatabricksSqlClient : IDatabricksClient
{
    private readonly string _connectionString;

    /// <param name="connectionString">
    /// Value of the <c>Databricks:ConnectionString</c> configuration key.
    /// Passing <c>null</c> or empty produces a clear <see cref="InvalidOperationException"/>
    /// at construction time rather than a cryptic runtime failure.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// Thrown immediately if <paramref name="connectionString"/> is null or whitespace,
    /// directing the caller to use the stub instead.
    /// </exception>
    public DatabricksSqlClient(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Databricks live client requires Databricks:ConnectionString; default is the stub.");

        _connectionString = connectionString;
    }

    /// <inheritdoc/>
    // Explicit interface implementation: keeps the public surface free of the internal
    // DatabricksInsightRow type while leaving the class itself public (for diagnostics/tests).
    Task<IReadOnlyList<DatabricksInsightRow>> IDatabricksClient.FetchInsightRowsAsync(
        string? cursor, CancellationToken ct)
    {
        // TODO: POST /api/2.0/sql/statements with _connectionString bearer token.
        // Build the HTTP call here when wiring for a real Databricks workspace.
        // This placeholder exists so intent is clear; it is never reached in stub-default mode.
        throw new NotImplementedException(
            "DatabricksSqlClient.FetchInsightRowsAsync is not yet implemented. " +
            "Set Databricks:Mode=Live and Databricks:ConnectionString to activate the live path.");
    }
}
