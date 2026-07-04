using ApiPlatform.Platform.Audit;
using ApiPlatform.Platform.Runtime;
using ApiPlatform.Platform.Telemetry;
using ApiPlatform.Poller;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddPlatformCore(builder.Configuration)
    // Lean OTel for an off-web AOT worker (no ASP.NET Core framework reference).
    .AddPlatformTelemetry("ApiPlatform.Poller")
    .AddSingleton<ICreationFeed, InMemoryCreationFeed>()
    .AddHostedService<RecordCreationPoller>();

// ── Cursor store: Memory (default, zero-config) or Durable (file-backed) ──────
var cursorStoreType = builder.Configuration["CURSOR_STORE"] ?? "Memory";
if (cursorStoreType.Equals("Durable", StringComparison.OrdinalIgnoreCase))
{
    var cursorDir = builder.Configuration["CURSOR_STORE_DIR"]
        ?? Path.Combine(Path.GetTempPath(), "poller-cursor");
    builder.Services.AddSingleton<ICursorStore>(sp =>
        new DurableFileCursorStore(cursorDir, sp.GetRequiredService<TimeProvider>()));
}
else
{
    builder.Services.AddSingleton<ICursorStore, InMemoryCursorStore>();
}

// AOT-safe audit sink: the default Audit.NET sink isn't native-AOT-compatible
// (Marshal.GetExceptionCode -> PlatformNotSupportedException), so this off-path AOT host
// overrides IPlatformAudit with a source-gen JSON sink. Masking is still inherited from core.
var auditDir = builder.Configuration["AUDIT_LOG_DIR"]
    ?? Path.Combine(Path.GetTempPath(), "audit-logs");
builder.Services.AddSingleton<IPlatformAudit>(sp => new JsonFileAuditSink(auditDir, PollerJson.Options, sp.GetRequiredService<TimeProvider>()));

var host = builder.Build();
await host.RunAsync();
