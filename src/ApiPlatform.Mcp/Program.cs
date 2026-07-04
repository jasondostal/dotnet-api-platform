using ApiPlatform.Integration.Runtime;
using ApiPlatform.Mcp;
using ApiPlatform.Platform.Runtime;
using ApiPlatform.ServiceDefaults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

// ── Inherited service defaults (OTel, health, resilience, discovery) ──────────
builder.AddServiceDefaults();

// ── Platform governance ───────────────────────────────────────────────────────
builder.Services.AddPlatformCore(builder.Configuration);

// ── Integration (account + customer sources) ──────────────────────────────────
builder.Services.AddIntegration(builder.Configuration);

// ── MCP server components ─────────────────────────────────────────────────────
builder.Services.AddSingleton<PlatformToolset>();
builder.Services.AddSingleton<ResourceCatalog>();
builder.Services.AddSingleton<McpServer>();

await builder.Build().RunAsync();
