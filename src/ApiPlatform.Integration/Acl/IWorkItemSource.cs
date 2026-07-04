using ApiPlatform.Contracts;
using ApiPlatform.Platform.Connectors;

namespace ApiPlatform.Integration.Acl;

/// <summary>
/// Canonical work-item source seam. Vendor connectors (e.g. ClickUp) implement this
/// and map their native shapes to the canonical <see cref="WorkItem"/> contract,
/// redacting PII through the platform redactor.
/// </summary>
public interface IWorkItemSource : IGovernedSource
{
    Task<WorkItemList> ListWorkItemsAsync(string? cursor = null, CancellationToken ct = default);

    Task<WorkItem?> GetWorkItemAsync(Guid workItemId, CancellationToken ct = default);
}
