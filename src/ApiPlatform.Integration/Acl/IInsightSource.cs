using ApiPlatform.Contracts;
using ApiPlatform.Platform.Connectors;

namespace ApiPlatform.Integration.Acl;

/// <summary>
/// Canonical insight/analytics source seam. The Databricks connector implements this
/// with a stub-default client; the live SQL client ships in-tree but stays unwired
/// unless explicitly configured.
/// </summary>
public interface IInsightSource : IGovernedSource
{
    Task<InsightList> ListInsightsAsync(string? cursor = null, CancellationToken ct = default);
}
