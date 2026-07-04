using ApiPlatform.Platform.Audit;
using ApiPlatform.Platform.Diagnostics;
using ApiPlatform.Platform.Pii;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ApiPlatform.Poller;

/// <summary>
/// Background service that polls the creation feed on a fixed interval.
/// Each newly-seen record is PII-masked and emitted as a platform audit event.
/// Tracing is wrapped in a <see cref="PlatformDiagnostics.ActivitySource"/> activity
/// so the work appears in distributed traces without web-host involvement.
/// </summary>
public sealed class RecordCreationPoller : BackgroundService
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(30);

    private readonly ICreationFeed _feed;
    private readonly ICursorStore _cursor;
    private readonly IPiiRedactor _redactor;
    private readonly IPlatformAudit _audit;
    private readonly ILogger<RecordCreationPoller> _logger;

    public RecordCreationPoller(
        ICreationFeed feed,
        ICursorStore cursor,
        IPiiRedactor redactor,
        IPlatformAudit audit,
        ILogger<RecordCreationPoller> logger)
    {
        _feed = feed;
        _cursor = cursor;
        _redactor = redactor;
        _audit = audit;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await PollOnceAsync(stoppingToken);

            try
            {
                await Task.Delay(DefaultInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown — exit the loop cleanly.
                break;
            }
        }
    }

    /// <summary>
    /// Runs one poll cycle: fetches new records since the current cursor, masks PII,
    /// records audit events, and advances the cursor. Exposed for direct test driving.
    ///
    /// Records are processed in groups keyed by <see cref="RecordCreated.CreatedAt"/>.
    /// The cursor is committed to a group's timestamp ONLY after every record in that
    /// group has been audited successfully — at-least-once delivery. If any audit in
    /// the group throws, the exception propagates and the cursor stays at its prior
    /// value so the next tick reprocesses the entire group.
    /// </summary>
    public async Task PollOnceAsync(CancellationToken ct = default)
    {
        using var activity = PlatformDiagnostics.ActivitySource.StartActivity(
            "RecordCreationPoller.PollOnce");

        var since = _cursor.GetCursor();

        // Buffer records so they can be grouped. A "group" is all records sharing
        // a CreatedAt timestamp; the cursor advances per-group, never per-record.
        var records = new List<RecordCreated>();
        await foreach (var record in _feed.GetRecordsSinceAsync(since, ct))
            records.Add(record);

        foreach (var group in records.GroupBy(r => r.CreatedAt).OrderBy(g => g.Key))
        {
            // ── Process every record in this group ───────────────────────────────
            // If any audit throws, the exception propagates out of the loop and
            // _cursor.Advance below is never reached — the group is not committed.
            foreach (var record in group)
            {
                var maskedEmail = _redactor.MaskEmail(record.Email);

                await _audit.RecordAsync(
                    "RecordCreated:Seen",
                    new RecordCreatedAudit(record.Id, maskedEmail, record.CreatedAt),
                    ct);

                PollerLog.ProcessedRecord(_logger, record.Id, record.CreatedAt);
            }

            // ── Group committed: all records audited — advance cursor ─────────────
            _cursor.Advance(group.Key);
            PollerLog.CursorAdvanced(_logger, group.Key);
        }
    }
}
