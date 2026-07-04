using Audit.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ApiPlatform.Platform.Audit;

/// <summary>
/// Configures the Audit.NET global data provider for the platform.
/// Call <see cref="Configure"/> once at startup before the DI container is built.
/// </summary>
public static class PlatformAudit
{
    /// <summary>
    /// Sets up Audit.NET to write JSON files to the directory specified by
    /// <c>AUDIT_LOG_DIR</c> configuration key, falling back to a temp sub-directory.
    /// Errors are non-fatal and emitted via <paramref name="logger"/> when provided.
    /// </summary>
    public static void Configure(IConfiguration configuration, ILogger? logger = null)
    {
        try
        {
            var auditDir = configuration["AUDIT_LOG_DIR"]
                ?? Path.Combine(Path.GetTempPath(), "audit-logs");

            Directory.CreateDirectory(auditDir);

            var tp = TimeProvider.System;
            Configuration.Setup()
                .UseFileLogProvider(opts => opts
                    .Directory(auditDir)
                    .FilenameBuilder(_ => $"audit-{tp.GetUtcNow():yyyyMMdd}.json"))
                .WithCreationPolicy(EventCreationPolicy.InsertOnStartReplaceOnEnd);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Audit.NET setup failed: {Message}", ex.Message);
        }
    }
}
