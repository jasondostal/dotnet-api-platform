using ApiPlatform.Contracts;
using ApiPlatform.Integration.Acl;

namespace ApiPlatform.Integration.Acl.Databricks;

/// <summary>
/// <see cref="IInsightSource"/> implementation backed by a Databricks client.
/// The client (<see cref="IDatabricksClient"/>) is resolved from DI; which concrete
/// implementation is registered is determined at startup by <see cref="DatabricksConnectorModule"/>
/// based on configuration (stub by default, live only when explicitly configured).
/// </summary>
internal sealed class DatabricksInsightSource : IInsightSource
{
    private readonly IDatabricksClient _client;

    public DatabricksInsightSource(IDatabricksClient client)
    {
        _client = client;
    }

    /// <inheritdoc/>
    public async Task<InsightList> ListInsightsAsync(string? cursor = null, CancellationToken ct = default)
    {
        var rows = await _client.FetchInsightRowsAsync(cursor, ct);

        return new InsightList
        {
            Data = rows.Select(r => new Insight
            {
                Metric    = r.Metric,
                Dimension = r.Dimension,
                Value     = r.Value,
                AsOf      = r.AsOf,
            }).ToList(),

            // Stub has no pagination; the live client would propagate the API's nextPageToken here.
            NextCursor = null,
        };
    }
}
