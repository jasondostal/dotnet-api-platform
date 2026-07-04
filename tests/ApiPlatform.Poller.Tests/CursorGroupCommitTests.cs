using System.Runtime.CompilerServices;
using ApiPlatform.Platform.Audit;
using ApiPlatform.Platform.Pii;
using ApiPlatform.Poller;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApiPlatform.Poller.Tests;

/// <summary>
/// Verifies the group-level commit invariant: a cursor position is advanced ONLY after
/// every record in the group for that timestamp has been successfully processed; on any
/// failure the cursor stays at its prior value and the next tick reprocesses the group.
/// Also covers the durable cursor round-trip (new instance reads persisted position).
/// </summary>
public sealed class CursorGroupCommitTests : IDisposable
{
    private readonly string _tmpDir = Path.Combine(
        Path.GetTempPath(), $"cursor-commit-tests-{Guid.NewGuid():N}");

    public CursorGroupCommitTests() => Directory.CreateDirectory(_tmpDir);

    public void Dispose()
    {
        if (Directory.Exists(_tmpDir))
            Directory.Delete(_tmpDir, recursive: true);
    }

    // ── Shared test timestamps ─────────────────────────────────────────────────

    private static readonly DateTimeOffset T1 =
        new(2024, 3, 1, 0, 0, 1, TimeSpan.Zero);

    private static readonly DateTimeOffset T2 =
        new(2024, 3, 1, 0, 0, 2, TimeSpan.Zero);

    // ── Stubs ──────────────────────────────────────────────────────────────────

    /// <summary>Feed backed by a fixed record list; filters by since like the production feed.</summary>
    private sealed class FixedFeed : ICreationFeed
    {
        private readonly RecordCreated[] _records;

        public FixedFeed(params RecordCreated[] records) => _records = records;

        public async IAsyncEnumerable<RecordCreated> GetRecordsSinceAsync(
            DateTimeOffset since,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            foreach (var r in _records.Where(r => r.CreatedAt > since))
            {
                ct.ThrowIfCancellationRequested();
                yield return r;
                await Task.Yield();
            }
        }
    }

    /// <summary>Audit stub that throws a deterministic exception on the Nth call.</summary>
    private sealed class ThrowOnNthAudit : IPlatformAudit
    {
        private int _callCount;
        private readonly int _failOnCall;

        public ThrowOnNthAudit(int failOnCall) => _failOnCall = failOnCall;

        public Task RecordAsync(string eventType, object data, CancellationToken ct = default)
        {
            if (++_callCount == _failOnCall)
                throw new InvalidOperationException(
                    $"Simulated audit failure on call {_failOnCall}.");
            return Task.CompletedTask;
        }
    }

    /// <summary>Audit stub that always succeeds.</summary>
    private sealed class NullAudit : IPlatformAudit
    {
        public Task RecordAsync(string eventType, object data, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private static RecordCreationPoller BuildPoller(
        ICreationFeed feed, ICursorStore cursor, IPlatformAudit audit)
    {
        var redactor = new DefaultPiiRedactor();
        var logger   = NullLogger<RecordCreationPoller>.Instance;
        return new RecordCreationPoller(feed, cursor, redactor, audit, logger);
    }

    // ── Test 1: partial group failure — cursor must NOT advance ───────────────

    [Fact]
    public async Task GroupCommit_AuditFailsOnSecondRecordInGroup_CursorDoesNotAdvance()
    {
        // Two records at T1 form a single group; one record at T2 forms the next group.
        var feed = new FixedFeed(
            new RecordCreated(Guid.NewGuid(), "a@example.com", T1),
            new RecordCreated(Guid.NewGuid(), "b@example.com", T1), // same group as above
            new RecordCreated(Guid.NewGuid(), "c@example.com", T2));

        var cursor = new InMemoryCursorStore();

        // Fails on the second audit call — the second record in the T1 group.
        var audit  = new ThrowOnNthAudit(failOnCall: 2);
        var poller = BuildPoller(feed, cursor, audit);

        // The poll throws because the audit failure propagates.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => poller.PollOnceAsync());

        // Cursor must NOT have advanced — the T1 group was only partially processed.
        Assert.Equal(DateTimeOffset.MinValue, cursor.GetCursor());
    }

    // ── Test 2: all groups succeed — cursor advances to latest ─────────────────

    [Fact]
    public async Task GroupCommit_AllGroupsSucceed_CursorAdvancesToLatestTimestamp()
    {
        var feed = new FixedFeed(
            new RecordCreated(Guid.NewGuid(), "a@example.com", T1),
            new RecordCreated(Guid.NewGuid(), "b@example.com", T2));

        var cursor = new InMemoryCursorStore();
        var poller = BuildPoller(feed, cursor, new NullAudit());

        await poller.PollOnceAsync();

        Assert.Equal(T2, cursor.GetCursor());
    }

    // ── Test 3: next tick reprocesses the failed group ────────────────────────

    [Fact]
    public async Task GroupCommit_AfterFailure_NextTickReprocessesEntireGroup()
    {
        // Same two-record group at T1.
        var rA = new RecordCreated(Guid.NewGuid(), "a@example.com", T1);
        var rB = new RecordCreated(Guid.NewGuid(), "b@example.com", T1);
        var rC = new RecordCreated(Guid.NewGuid(), "c@example.com", T2);
        var feed = new FixedFeed(rA, rB, rC);

        var cursor        = new InMemoryCursorStore();
        var failingAudit  = new ThrowOnNthAudit(failOnCall: 2);
        var pollerFailing = BuildPoller(feed, cursor, failingAudit);

        // First tick: fails mid-group, cursor stays at MinValue.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => pollerFailing.PollOnceAsync());

        Assert.Equal(DateTimeOffset.MinValue, cursor.GetCursor());

        // Second tick with a successful audit: both T1 records are reprocessed.
        var successAudit   = new NullAudit();
        var pollerSuccess  = BuildPoller(feed, cursor, successAudit);
        await pollerSuccess.PollOnceAsync();

        // Cursor now at T2 (all groups fully committed).
        Assert.Equal(T2, cursor.GetCursor());
    }

    // ── Test 4: durable cursor round-trip ────────────────────────────────────

    [Fact]
    public void DurableCursorStore_Advance_SurvivesNewInstance()
    {
        var dir = Path.Combine(_tmpDir, "cursor");
        var tp  = TimeProvider.System;

        // First instance (simulating current process run): write T2.
        var store1 = new DurableFileCursorStore(dir, tp);
        store1.Advance(T2);

        // Second instance (simulating a restarted process over the same dir): must read T2.
        var store2 = new DurableFileCursorStore(dir, tp);
        Assert.Equal(T2, store2.GetCursor());
    }

    // ── Test 5: durable cursor does not regress ────────────────────────────────

    [Fact]
    public void DurableCursorStore_Advance_DoesNotRegress()
    {
        var dir    = Path.Combine(_tmpDir, "cursor-no-regress");
        var tp     = TimeProvider.System;
        var store  = new DurableFileCursorStore(dir, tp);

        store.Advance(T2);
        store.Advance(T1); // T1 < T2 — should be ignored

        Assert.Equal(T2, store.GetCursor());
    }
}
