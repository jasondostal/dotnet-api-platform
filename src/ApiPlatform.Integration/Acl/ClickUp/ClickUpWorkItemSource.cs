using ApiPlatform.Contracts;
using ApiPlatform.Platform.Pii;

namespace ApiPlatform.Integration.Acl.ClickUp;

/// <summary>
/// IWorkItemSource backed by ClickUp (stub mode by default).
/// Translates vendor-native task shapes to the canonical <see cref="WorkItem"/> contract
/// and masks every assignee email through <see cref="IPiiRedactor"/> so callers
/// never receive a raw vendor email address.
/// </summary>
internal sealed class ClickUpWorkItemSource : IWorkItemSource
{
    private readonly StubClickUpClient _client;
    private readonly IPiiRedactor      _redactor;

    public ClickUpWorkItemSource(StubClickUpClient client, IPiiRedactor redactor)
    {
        _client   = client;
        _redactor = redactor;
    }

    public Task<WorkItemList> ListWorkItemsAsync(string? cursor = null, CancellationToken ct = default)
    {
        var items = _client.GetTasks()
            .Select(Map)
            .ToList();

        // Cursor-based pagination is a no-op in stub mode (all tasks fit in one page).
        return Task.FromResult(new WorkItemList { Data = items, NextCursor = null });
    }

    public Task<WorkItem?> GetWorkItemAsync(Guid workItemId, CancellationToken ct = default)
    {
        var raw = _client.GetTasks()
            .FirstOrDefault(t => _client.ResolveId(t.task_id) == workItemId);

        WorkItem? result = raw is null ? null : Map(raw);
        return Task.FromResult(result);
    }

    // ── Raw → canonical mapping ───────────────────────────────────────────────

    private WorkItem Map(StubClickUpClient.RawTask raw) => new()
    {
        WorkItemId    = _client.ResolveId(raw.task_id),
        Title         = raw.name,
        Status        = MapStatus(raw.status),
        // Mask PII at the ACL boundary — callers never see the raw vendor email.
        AssigneeEmail = string.IsNullOrEmpty(raw.assignee_email)
            ? null
            : _redactor.MaskEmail(raw.assignee_email),
    };

    private static WorkItemStatus MapStatus(string status) => status switch
    {
        "in_progress" => WorkItemStatus.IN_PROGRESS,
        "done"        => WorkItemStatus.DONE,
        _             => WorkItemStatus.OPEN,
    };
}
