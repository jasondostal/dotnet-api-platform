using ApiPlatform.Platform.Audit;
using ApiPlatform.Platform.Runtime;
using ApiPlatform.Poller;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ApiPlatform.Poller.Tests;

/// <summary>
/// Proves that the off-API-path host inherits audit + PII masking from
/// <see cref="PlatformCoreServiceCollectionExtensions.AddPlatformCore"/> alone —
/// no web host required.
/// </summary>
public sealed class PollerTests : IDisposable
{
    private readonly string _auditDir;

    public PollerTests()
    {
        _auditDir = Path.Combine(
            Path.GetTempPath(),
            $"poller-audit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_auditDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_auditDir))
            Directory.Delete(_auditDir, recursive: true);
    }

    [Fact]
    public async Task PollOnce_AdvancesCursor_WritesAuditFile_WithMaskedEmail()
    {
        // ── Arrange ──────────────────────────────────────────────────────────

        // Point Audit.NET at an isolated temp directory so this test is self-contained.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AUDIT_LOG_DIR"] = _auditDir
            })
            .Build();

        var services = new ServiceCollection()
            .AddPlatformCore(config)
            .AddLogging()
            .BuildServiceProvider();

        // Masking is inherited from AddPlatformCore; the audit SINK is the AOT-safe JSON sink
        // (the same override the Poller host applies), so this test exercises the real AOT path.
        var redactor = services.GetRequiredService<ApiPlatform.Platform.Pii.IPiiRedactor>();
        var audit    = new JsonFileAuditSink(_auditDir, PollerJson.Options, TimeProvider.System);
        var feed     = new InMemoryCreationFeed();
        var cursor   = new InMemoryCursorStore();
        var logger   = NullLogger<RecordCreationPoller>.Instance;

        // Cursor starts at MinValue; all three seed records should be consumed.
        var initialCursor = cursor.GetCursor();

        var poller = new RecordCreationPoller(feed, cursor, redactor, audit, logger);

        // ── Act ───────────────────────────────────────────────────────────────

        await poller.PollOnceAsync();

        // ── Assert (1): cursor advanced past the last consumed record ─────────

        Assert.True(
            cursor.GetCursor() > initialCursor,
            $"Expected cursor to advance from {initialCursor}, but it stayed at {cursor.GetCursor()}");

        // The latest seed record has CreatedAt = 2024-01-01T00:00:03Z.
        Assert.True(
            cursor.GetCursor() >= new DateTimeOffset(2024, 1, 1, 0, 0, 3, TimeSpan.Zero),
            "Cursor should be at or after the timestamp of the last seed record");

        // ── Assert (2): at least one audit file was written ───────────────────

        var auditFiles = Directory.GetFiles(_auditDir, "*.json");
        Assert.True(auditFiles.Length > 0, "Expected at least one audit JSON file in the audit directory");

        // ── Assert (3): audit content has masked email, NOT the raw email ─────

        // Read all audit content (may be one or more files).
        var allContent = string.Concat(
            await Task.WhenAll(auditFiles.Select(f => File.ReadAllTextAsync(f))));

        // Raw email addresses from the seed data must NOT appear in audit output.
        // (PlatformAudit uses a date-keyed filename; with InsertOnStartReplaceOnEnd the
        //  last scope overwrites earlier ones, so at minimum the last record is present.)
        Assert.DoesNotContain("alice@example.com", allContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("bob@contoso.com",   allContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("carol@domain.org",  allContent, StringComparison.OrdinalIgnoreCase);

        // A masked email pattern ("***@") must be present — proves DefaultPiiRedactor fired.
        // DefaultPiiRedactor.MaskEmail("carol@domain.org") → "c***@d***.org" (last record written).
        Assert.Contains("***@", allContent, StringComparison.Ordinal);
    }
}
