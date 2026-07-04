using System.Text.Json.Serialization;

namespace ApiPlatform.Contracts;

// ── Work-item models (mirrors /spec/workitems.tsp) ────────────────────────────

[JsonConverter(typeof(JsonStringEnumConverter<WorkItemStatus>))]
public enum WorkItemStatus { OPEN, IN_PROGRESS, DONE }

public class WorkItem
{
    public Guid WorkItemId { get; set; }
    public string Title { get; set; } = string.Empty;
    public WorkItemStatus Status { get; set; }
    // PII — masked unless the caller is entitled; exercised by the ClickUp connector.
    public string? AssigneeEmail { get; set; }
}

public class WorkItemList
{
    public List<WorkItem> Data { get; set; } = [];
    public string? NextCursor { get; set; }
}
