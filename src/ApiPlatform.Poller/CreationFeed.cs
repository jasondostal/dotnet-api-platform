using System.Runtime.CompilerServices;

namespace ApiPlatform.Poller;

/// <summary>
/// Represents a record that was newly created in the source system.
/// The <see cref="Email"/> field is PII and must be masked before audit.
/// </summary>
public sealed record RecordCreated(Guid Id, string Email, DateTimeOffset CreatedAt);

/// <summary>
/// Provides newly-created records that arrived after a given cursor position.
/// </summary>
public interface ICreationFeed
{
    /// <summary>
    /// Streams records whose <see cref="RecordCreated.CreatedAt"/> is strictly after
    /// <paramref name="since"/>.
    /// </summary>
    IAsyncEnumerable<RecordCreated> GetRecordsSinceAsync(
        DateTimeOffset since,
        CancellationToken ct = default);
}

/// <summary>
/// Stub in-memory feed with a fixed set of seeded records.
/// Useful for local development and integration tests.
/// </summary>
public sealed class InMemoryCreationFeed : ICreationFeed
{
    // Three seeded records with distinct creation timestamps.
    private static readonly RecordCreated[] SeedRecords =
    [
        new(Guid.Parse("a1b2c3d4-0001-0000-0000-000000000000"),
            "alice@example.com",
            new DateTimeOffset(2024, 1, 1, 0, 0, 1, TimeSpan.Zero)),

        new(Guid.Parse("a1b2c3d4-0002-0000-0000-000000000000"),
            "bob@contoso.com",
            new DateTimeOffset(2024, 1, 1, 0, 0, 2, TimeSpan.Zero)),

        new(Guid.Parse("a1b2c3d4-0003-0000-0000-000000000000"),
            "carol@domain.org",
            new DateTimeOffset(2024, 1, 1, 0, 0, 3, TimeSpan.Zero)),
    ];

    /// <inheritdoc />
    public async IAsyncEnumerable<RecordCreated> GetRecordsSinceAsync(
        DateTimeOffset since,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var record in SeedRecords.Where(r => r.CreatedAt > since))
        {
            ct.ThrowIfCancellationRequested();
            yield return record;
            await Task.Yield();
        }
    }
}
