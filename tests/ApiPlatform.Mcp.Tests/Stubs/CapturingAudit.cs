using ApiPlatform.Platform.Audit;

namespace ApiPlatform.Mcp.Tests.Stubs;

/// <summary>
/// Test double for <see cref="IPlatformAudit"/> that captures all emitted events
/// in memory so tests can assert audit behaviour without touching the filesystem.
/// </summary>
internal sealed class CapturingAudit : IPlatformAudit
{
    private readonly List<(string EventType, object Data)> _events = [];

    public IReadOnlyList<(string EventType, object Data)> Events => _events.AsReadOnly();

    public Task RecordAsync(string eventType, object data, CancellationToken ct = default)
    {
        _events.Add((eventType, data));
        return Task.CompletedTask;
    }
}
