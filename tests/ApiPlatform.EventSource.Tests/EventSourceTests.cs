using ApiPlatform.EventSource;
using ApiPlatform.EventSource.Models;
using ApiPlatform.Platform.Audit;
using ApiPlatform.Platform.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApiPlatform.EventSource.Tests;

/// <summary>
/// Exercises the WorkItemEventEmitter pipeline end-to-end using stub dependencies.
/// </summary>
public class EventSourceTests
{
    // Fixed sequence used by all three tests so order assertions are deterministic.
    private static readonly WorkItemChanged[] SampleChanges =
    [
        new() { WorkItemId = Guid.NewGuid(), ChangeType = "Created", At = DateTimeOffset.UtcNow },
        new() { WorkItemId = Guid.NewGuid(), ChangeType = "Updated", At = DateTimeOffset.UtcNow.AddSeconds(1) },
        new() { WorkItemId = Guid.NewGuid(), ChangeType = "Closed",  At = DateTimeOffset.UtcNow.AddSeconds(2) },
    ];

    // Lightweight no-op audit used in tests that do not verify the audit file.
    private sealed class NullAuditImpl : IPlatformAudit
    {
        public Task RecordAsync(string eventType, object data, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private static WorkItemEventEmitter BuildEmitter(
        IEnumerable<WorkItemChanged> changes,
        InMemoryEventSink            sink,
        IPlatformAudit               audit,
        IEventSourcePositionStore?   positionStore = null)
    {
        var feed   = new InMemoryWorkItemChangeFeed(changes);
        var logger = NullLogger<WorkItemEventEmitter>.Instance;
        return new WorkItemEventEmitter(
            feed, sink, audit,
            positionStore ?? new InMemoryEventSourcePositionStore(),
            logger);
    }

    // ── 1. Each change produces exactly one emitted event ──────────────────────

    [Fact]
    public async Task EachChange_ProducesExactlyOneEmittedEvent()
    {
        var sink    = new InMemoryEventSink();
        var emitter = BuildEmitter(SampleChanges, sink, new NullAuditImpl());

        await emitter.StartAsync(CancellationToken.None);
        await emitter.ExecuteTask!;
        await emitter.StopAsync(CancellationToken.None);

        Assert.Equal(SampleChanges.Length, sink.Emitted.Count);
    }

    // ── 2. Change types reach the sink in the original order ───────────────────

    [Fact]
    public async Task ChangeTypes_ArePreservedInOrder()
    {
        var sink    = new InMemoryEventSink();
        var emitter = BuildEmitter(SampleChanges, sink, new NullAuditImpl());

        await emitter.StartAsync(CancellationToken.None);
        await emitter.ExecuteTask!;
        await emitter.StopAsync(CancellationToken.None);

        var expected = SampleChanges.Select(c => c.ChangeType).ToArray();
        var actual   = sink.Emitted.Select(c => c.ChangeType).ToArray();
        Assert.Equal(expected, actual);
    }

    // ── 3. An audit file is created in the AUDIT_LOG_DIR set by AddPlatformCore ─

    [Fact]
    public async Task AuditFile_IsWritten_WhenChangesAreEmitted()
    {
        var auditDir = Path.Combine(
            Path.GetTempPath(), $"eventsource-audit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(auditDir);

        try
        {
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AUDIT_LOG_DIR"] = auditDir
                })
                .Build();

            var services = new ServiceCollection();
            services.AddPlatformCore(config);
            await using var sp = services.BuildServiceProvider();

            var audit   = sp.GetRequiredService<IPlatformAudit>();
            var sink    = new InMemoryEventSink();
            var emitter = BuildEmitter(SampleChanges, sink, audit);

            await emitter.StartAsync(CancellationToken.None);
            await emitter.ExecuteTask!;
            await emitter.StopAsync(CancellationToken.None);

            var files = Directory.GetFiles(auditDir);
            Assert.True(
                files.Length > 0,
                $"Expected at least one audit file in {auditDir} but found none.");
        }
        finally
        {
            if (Directory.Exists(auditDir))
                Directory.Delete(auditDir, recursive: true);
        }
    }
}
