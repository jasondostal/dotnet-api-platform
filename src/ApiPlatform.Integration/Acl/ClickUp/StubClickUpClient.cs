namespace ApiPlatform.Integration.Acl.ClickUp;

/// <summary>
/// In-memory stub for the ClickUp work-item API.
/// Returns seed tasks in vendor-native shape to exercise the ACL mapping and PII redaction seams.
/// Swap for a real HTTP client without changing any source or connector code.
/// </summary>
internal sealed class StubClickUpClient
{
    // ── Vendor-native DTOs (internal — never exposed beyond this folder) ──────

    internal sealed record RawTask(
        string task_id,           // vendor-native string id, e.g. "cu-0001"
        string name,              // task title
        string status,            // "open" | "in_progress" | "done"
        string? assignee_email    // real-looking email; exercised by redaction seam
    );

    // ── Deterministic vendor-id → canonical UUID mapping ─────────────────────

    private static readonly IReadOnlyDictionary<string, Guid> TaskIdMap =
        new Dictionary<string, Guid>
        {
            ["cu-0001"] = Guid.Parse("c1000001-0000-4000-8000-000000000001"),
            ["cu-0002"] = Guid.Parse("c1000002-0000-4000-8000-000000000002"),
            ["cu-0003"] = Guid.Parse("c1000003-0000-4000-8000-000000000003"),
        };

    // ── Seed tasks ────────────────────────────────────────────────────────────

    private static readonly IReadOnlyList<RawTask> SeedTasks =
    [
        new RawTask(
            task_id        : "cu-0001",
            name           : "Implement OAuth token refresh",
            status         : "open",
            assignee_email : "jane.doe@example.com"
        ),
        new RawTask(
            task_id        : "cu-0002",
            name           : "Write API spec for /workitems",
            status         : "in_progress",
            assignee_email : "alex.smith@clickup-test.io"
        ),
        new RawTask(
            task_id        : "cu-0003",
            name           : "Deploy staging environment",
            status         : "done",
            assignee_email : null
        ),
    ];

    internal IReadOnlyList<RawTask> GetTasks() => SeedTasks;

    /// <summary>Returns the canonical UUID for a vendor task id, or a new UUID as fallback.</summary>
    internal Guid ResolveId(string taskId) =>
        TaskIdMap.TryGetValue(taskId, out var id) ? id : Guid.NewGuid();
}
