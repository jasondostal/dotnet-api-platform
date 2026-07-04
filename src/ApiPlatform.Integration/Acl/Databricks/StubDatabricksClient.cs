namespace ApiPlatform.Integration.Acl.Databricks;

/// <summary>
/// In-memory stub returning fixture insight rows.
/// This is the <b>default</b> Databricks client — no external subscription or credentials required.
/// Swap for <see cref="DatabricksSqlClient"/> only by setting <c>Databricks:Mode=Live</c>
/// and providing <c>Databricks:ConnectionString</c>.
/// </summary>
internal sealed class StubDatabricksClient : IDatabricksClient
{
    private static readonly IReadOnlyList<DatabricksInsightRow> SeedRows =
    [
        new DatabricksInsightRow("accounts.opened",    "retail",   142m,       new DateOnly(2026, 6, 1)),
        new DatabricksInsightRow("balance.avg",        "retail",   4_318.75m,  new DateOnly(2026, 6, 1)),
        new DatabricksInsightRow("accounts.opened",    "business",  23m,        new DateOnly(2026, 6, 1)),
        new DatabricksInsightRow("balance.avg",        "business",  31_450.00m, new DateOnly(2026, 6, 1)),
        new DatabricksInsightRow("loan.originations",  null,        58m,        new DateOnly(2026, 6, 1)),
    ];

    public Task<IReadOnlyList<DatabricksInsightRow>> FetchInsightRowsAsync(
        string? cursor = null, CancellationToken ct = default)
        => Task.FromResult(SeedRows);
}
