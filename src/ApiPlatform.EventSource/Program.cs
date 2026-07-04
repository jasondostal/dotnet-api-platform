using ApiPlatform.EventSource;
using ApiPlatform.Platform.Runtime;
using ApiPlatform.ServiceDefaults;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddPlatformCore(builder.Configuration);

builder.Services.AddSingleton<IWorkItemChangeFeed, InMemoryWorkItemChangeFeed>();
builder.Services.AddSingleton<IEventSink, InMemoryEventSink>();

// ── Position store: Memory (default, zero-config) or Durable (file-backed) ────
var positionStoreType = builder.Configuration["EVENTSOURCE_POSITION_STORE"] ?? "Memory";
if (positionStoreType.Equals("Durable", StringComparison.OrdinalIgnoreCase))
{
    var posDir = builder.Configuration["EVENTSOURCE_POSITION_DIR"]
        ?? Path.Combine(Path.GetTempPath(), "eventsource-position");
    builder.Services.AddSingleton<IEventSourcePositionStore>(
        _ => new DurableFileEventSourcePositionStore(posDir));
}
else
{
    builder.Services.AddSingleton<IEventSourcePositionStore, InMemoryEventSourcePositionStore>();
}

builder.Services.AddHostedService<WorkItemEventEmitter>();

await builder.Build().RunAsync();
