using ApiPlatform.Platform.Audit;
using ApiPlatform.Platform.Pii;
using ApiPlatform.Platform.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Tests;

/// <summary>
/// Verifies that the platform governance core works from a plain runtime context —
/// no web host, no ASP.NET middleware.
/// </summary>
public class PlatformCoreRuntimeTests
{
    [Fact]
    public async Task OffPathJob_IsAuditedAndMasked_FromRuntimeOnly()
    {
        // Arrange: isolated temp dir per test run so audit files don't bleed across runs
        var auditDir = Path.Combine(
            Path.GetTempPath(),
            $"platform-audit-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(auditDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AUDIT_LOG_DIR"] = auditDir
            })
            .Build();

        var services = new ServiceCollection();
        services.AddPlatformCore(config);
        await using var provider = services.BuildServiceProvider();

        // Act — PII masking
        var redactor = provider.GetRequiredService<IPiiRedactor>();
        var maskedEmail = redactor.MaskEmail("jane@example.com");
        var maskedPhone = redactor.MaskPhone("555-123-4567");

        // Assert — masking produces expected patterns
        Assert.Equal("j***@e***.com", maskedEmail);
        Assert.Equal("***-***-4567", maskedPhone);

        // Act — audit recording
        var audit = provider.GetRequiredService<IPlatformAudit>();
        await audit.RecordAsync("OffPathJob:Run", new { id = 1 });

        // Assert — at least one audit file was written to the temp dir
        var files = Directory.GetFiles(auditDir, "audit-*.json");
        Assert.NotEmpty(files);
    }
}
