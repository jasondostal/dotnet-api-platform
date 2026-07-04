using ApiPlatform.EventSource.Models;
using ApiPlatform.Platform.Audit;
using ApiPlatform.Platform.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApiPlatform.EventSource;

/// <summary>
/// Hosted background service that consumes <see cref="IWorkItemChangeFeed"/> and,
/// for each change, emits a domain event to <see cref="IEventSink"/> and records
/// an audit trail entry via <see cref="IPlatformAudit"/>.
///
/// Changes are processed in groups: all changes sharing the same
/// <see cref="WorkItemChanged.At"/> timestamp form a group. The position is advanced
/// to a group's timestamp ONLY after every change in that group has been emitted
/// successfully — at-least-once delivery. If any emission in the group fails, the
/// exception propagates and the position is unchanged so the next run reprocesses
/// the group from the beginning.
/// </summary>
public sealed class WorkItemEventEmitter : BackgroundService
{
    private readonly IWorkItemChangeFeed          _feed;
    private readonly IEventSink                   _sink;
    private readonly IPlatformAudit               _audit;
    private readonly IEventSourcePositionStore    _positionStore;
    private readonly ILogger<WorkItemEventEmitter> _logger;

    public WorkItemEventEmitter(
        IWorkItemChangeFeed           feed,
        IEventSink                    sink,
        IPlatformAudit                audit,
        IEventSourcePositionStore     positionStore,
        ILogger<WorkItemEventEmitter> logger)
    {
        _feed          = feed;
        _sink          = sink;
        _audit         = audit;
        _positionStore = positionStore;
        _logger        = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var position = _positionStore.GetPosition();

        // Track the current group: consecutive changes with the same At timestamp.
        // Group boundaries are detected when the At timestamp changes.
        DateTimeOffset?          currentGroupAt = null;
        var                      currentGroup   = new List<WorkItemChanged>();

        await foreach (var change in _feed.StreamAsync(stoppingToken))
        {
            // Skip changes already processed in a prior run (At <= last committed position).
            if (change.At <= position)
                continue;

            if (currentGroupAt.HasValue && change.At != currentGroupAt.Value)
            {
                // A new timestamp was seen: commit the completed previous group first.
                // If EmitGroupAsync throws, the exception propagates and the position
                // is NOT advanced — the previous group will be retried next run.
                await EmitGroupAsync(currentGroup, stoppingToken);
                _positionStore.Advance(currentGroupAt.Value);
                currentGroup.Clear();
            }

            currentGroupAt = change.At;
            currentGroup.Add(change);
        }

        // Commit the final group when the stream ends cleanly (finite feed or graceful stop).
        if (currentGroup.Count > 0 && currentGroupAt.HasValue)
        {
            await EmitGroupAsync(currentGroup, stoppingToken);
            _positionStore.Advance(currentGroupAt.Value);
        }
    }

    /// <summary>
    /// Emits all changes in <paramref name="group"/> to the sink and records audit events.
    /// If any emission throws, the exception propagates — the group's position is NOT advanced.
    /// </summary>
    private async Task EmitGroupAsync(List<WorkItemChanged> group, CancellationToken ct)
    {
        foreach (var change in group)
        {
            using var activity = PlatformDiagnostics.ActivitySource.StartActivity("WorkItemChanged");
            activity?.SetTag("workItemId", change.WorkItemId.ToString());
            activity?.SetTag("changeType", change.ChangeType);

            await _sink.EmitAsync(change, ct);
            await _audit.RecordAsync("WorkItemChanged", change, ct);

            EventSourceLog.EmittedChange(_logger, change.ChangeType, change.WorkItemId);
        }
    }
}
