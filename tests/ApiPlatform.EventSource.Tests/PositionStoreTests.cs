using ApiPlatform.EventSource;
using ApiPlatform.EventSource.Models;
using ApiPlatform.Platform.Audit;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApiPlatform.EventSource.Tests;

/// <summary>
/// Verifies the EventSource position store and the group-level commit invariant:
/// the position advances ONLY after the full group is emitted; on any sink failure
/// the position stays at its prior value so the next run reprocesses the group.
/// Also covers the durable position round-trip.
/// </summary>
public sealed class PositionStoreTests : IDisposable
{
    private readonly string _tmpDir = Path.Combine(
        Path.GetTempPath(), $"eventsource-pos-tests-{Guid.NewGuid():N}");

    public PositionStoreTests() => Directory.CreateDirectory(_tmpDir);

    public void Dispose()
    {
        if (Directory.Exists(_tmpDir))
            Directory.Delete(_tmpDir, recursive: true);
    }

    // Timestamps chosen well away from DateTimeOffset.MinValue/MaxValue extremes.
    private static readonly DateTimeOffset T1 = DateTimeOffset.UnixEpoch.AddHours(1);
    private static readonly DateTimeOffset T2 = DateTimeOffset.UnixEpoch.AddHours(2);

    // ── Stubs ──────────────────────────────────────────────────────────────────

    /// <summary>Sink that throws a deterministic exception on the Nth emit call.</summary>
    private sealed class ThrowOnNthSink : IEventSink
    {
        private int _callCount;
        private readonly int _failOnCall;

        public ThrowOnNthSink(int failOnCall) => _failOnCall = failOnCall;

        public ValueTask EmitAsync(WorkItemChanged change, CancellationToken ct = default)
        {
            if (++_callCount == _failOnCall)
                throw new InvalidOperationException(
                    $"Simulated sink failure on call {_failOnCall}.");
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Audit stub that always succeeds.</summary>
    private sealed class NullAudit : IPlatformAudit
    {
        public Task RecordAsync(string eventType, object data, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private static WorkItemEventEmitter BuildEmitter(
        IEnumerable<WorkItemChanged>    changes,
        IEventSink                      sink,
        IEventSourcePositionStore       positionStore)
    {
        return new WorkItemEventEmitter(
            new InMemoryWorkItemChangeFeed(changes),
            sink,
            new NullAudit(),
            positionStore,
            NullLogger<WorkItemEventEmitter>.Instance);
    }

    // ── Test 1: durable position store round-trip ──────────────────────────────

    [Fact]
    public void DurablePositionStore_Advance_SurvivesNewInstance()
    {
        var dir = Path.Combine(_tmpDir, "position");

        // First instance: commit T1.
        var store1 = new DurableFileEventSourcePositionStore(dir);
        store1.Advance(T1);

        // Second instance (simulating a restarted process): must read T1.
        var store2 = new DurableFileEventSourcePositionStore(dir);
        Assert.Equal(T1, store2.GetPosition());
    }

    // ── Test 2: durable position does not regress ─────────────────────────────

    [Fact]
    public void DurablePositionStore_Advance_DoesNotRegress()
    {
        var dir   = Path.Combine(_tmpDir, "position-no-regress");
        var store = new DurableFileEventSourcePositionStore(dir);

        store.Advance(T2);
        store.Advance(T1); // T1 < T2 — must be ignored

        Assert.Equal(T2, store.GetPosition());
    }

    // ── Test 3: group commit — sink failure prevents position advancement ──────

    [Fact]
    public async Task GroupCommit_SinkFailsOnSecondChangeInGroup_PositionDoesNotAdvance()
    {
        // Two changes at T1 form a single group; one at T2 forms the next group.
        var changes = new[]
        {
            new WorkItemChanged { WorkItemId = Guid.NewGuid(), ChangeType = "Created", At = T1 },
            new WorkItemChanged { WorkItemId = Guid.NewGuid(), ChangeType = "Created", At = T1 },
            new WorkItemChanged { WorkItemId = Guid.NewGuid(), ChangeType = "Created", At = T2 },
        };

        var positionStore = new InMemoryEventSourcePositionStore();
        // Fails on the second emit call — the second change in the T1 group.
        var sink          = new ThrowOnNthSink(failOnCall: 2);
        var emitter       = BuildEmitter(changes, sink, positionStore);

        await emitter.StartAsync(CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await emitter.ExecuteTask!);

        // Position must NOT have advanced — the T1 group was not fully committed.
        Assert.Equal(DateTimeOffset.MinValue, positionStore.GetPosition());
    }

    // ── Test 4: in-memory default — zero config, all changes emitted ─────────

    [Fact]
    public async Task InMemoryDefault_AllChangesEmitted_PositionAdvancesToLatest()
    {
        var changes = new[]
        {
            new WorkItemChanged { WorkItemId = Guid.NewGuid(), ChangeType = "Created", At = T1 },
            new WorkItemChanged { WorkItemId = Guid.NewGuid(), ChangeType = "Updated", At = T2 },
        };

        var sink          = new InMemoryEventSink();
        var positionStore = new InMemoryEventSourcePositionStore();
        var emitter       = BuildEmitter(changes, sink, positionStore);

        await emitter.StartAsync(CancellationToken.None);
        await emitter.ExecuteTask!;
        await emitter.StopAsync(CancellationToken.None);

        Assert.Equal(2, sink.Emitted.Count);
        Assert.Equal(T2, positionStore.GetPosition());
    }
}
