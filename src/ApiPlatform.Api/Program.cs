using System.Text.Json;
using System.Text.Json.Serialization;
using ApiPlatform.Api.Endpoints;
using ApiPlatform.Api.Eventing;
using ApiPlatform.Integration.Runtime;
using ApiPlatform.Platform.AspNetCore.Runtime;
using ApiPlatform.ServiceDefaults;
using Asp.Versioning;
using Audit.Core;
using Audit.WebApi;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

// ── Platform governance (auth, problem details, idempotency, core DI) ─────────
builder.AddPlatform();

// ── JSON serialization ────────────────────────────────────────────────────────
builder.Services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    // Source-generated metadata first (reflection-free / AOT-ready for the canonical types);
    // anonymous/unlisted payloads fall through to the reflection resolver.
    opts.SerializerOptions.TypeInfoResolverChain.Insert(0, ApiPlatformJsonContext.Default);
});

// ── API Versioning ────────────────────────────────────────────────────────────
builder.Services.AddApiVersioning(opts =>
{
    opts.DefaultApiVersion = new ApiVersion(1, 0);
    opts.AssumeDefaultVersionWhenUnspecified = true;
    opts.ReportApiVersions = true;
    opts.ApiVersionReader = new UrlSegmentApiVersionReader();
});

// ── OpenAPI ───────────────────────────────────────────────────────────────────
builder.Services.AddOpenApi();

// ── Healthchecks ──────────────────────────────────────────────────────────────
builder.Services.AddHealthChecks();

// ── Integration layer (ACL connectors + routing) ──────────────────────────────
// Self-registers all vendor connector modules and wires the routing aggregator.
builder.Services.AddIntegration(builder.Configuration);

// ── Eventing ──────────────────────────────────────────────────────────────────
// EVENT_PUBLISHER_TYPE = InMemory (default) | EventGrid
// EventGrid + non-Development + absent config → InvalidOperationException (fail-fast).
builder.Services.AddApiEventPublisher(builder.Configuration, builder.Environment);
builder.Services.AddSingleton<ReceivedEventLog>();

// ── OpenTelemetry + service defaults (OTel, health, resilience, discovery) ────
builder.AddServiceDefaults();

// ── Azure Monitor (gated on connection string) ────────────────────────────────
// Builder-phase exceptions are captured here and logged below after app.Build()
// when the ILogger pipeline is available.
Exception? azureMonitorEx = null;
var aiConnStr = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrWhiteSpace(aiConnStr))
{
    try
    {
        builder.Services.AddOpenTelemetry().UseAzureMonitor();
    }
    catch (Exception ex)
    {
        azureMonitorEx = ex;
    }
}

// ── Audit.NET ─────────────────────────────────────────────────────────────────
Exception? auditSetupEx = null;
try
{
    // Writable path: the container runs as a non-root user and cannot write under /app.
    // Defaults to a temp dir; override with AUDIT_LOG_DIR. (A real deployment points the
    // audit face at a durable, append-only store — see ARCHITECTURE §6.)
    var auditDir = builder.Configuration["AUDIT_LOG_DIR"]
        ?? Path.Combine(Path.GetTempPath(), "audit-logs");
    Directory.CreateDirectory(auditDir);
    var auditTp = TimeProvider.System;
    Audit.Core.Configuration.Setup()
        .UseFileLogProvider(opts => opts
            .Directory(auditDir)
            .FilenameBuilder(_ => $"audit-{auditTp.GetUtcNow():yyyyMMdd}.json"))
        .WithCreationPolicy(EventCreationPolicy.InsertOnStartReplaceOnEnd);

    builder.Services.AddMvc(mvc =>
    {
        mvc.AddAuditFilter(config => config
            .LogAllActions()
            .WithEventType("WebApi:{verb}:{controller}")
            .IncludeResponseBody()
            .IncludeRequestBody());
    });
}
catch (Exception ex)
{
    auditSetupEx = ex;
}

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();
// ─────────────────────────────────────────────────────────────────────────────

// Emit any non-fatal builder-phase warnings now that the ILogger pipeline is live.
if (azureMonitorEx is not null)
    app.Logger.LogWarning(azureMonitorEx,
        "Azure Monitor registration failed: {Message}", azureMonitorEx.Message);
if (auditSetupEx is not null)
    app.Logger.LogWarning(auditSetupEx,
        "Audit.NET setup failed: {Message}", auditSetupEx.Message);

// ── Platform middleware (exception handler, auth, idempotency, …) ─────────────
app.UsePlatform();

// Serve OpenAPI JSON unconditionally so drift-detection scripts and integration
// tests can reach /openapi/v1.json outside Development mode.
app.MapOpenApi();

// ── Healthcheck ───────────────────────────────────────────────────────────────
app.MapHealthChecks("/health");

// ── Root links payload ────────────────────────────────────────────────────────
app.MapGet("/", () => Results.Ok(new
{
    service = "ApiPlatform.Api",
    version = "1.0",
    links = new[]
    {
        new { rel = "accounts",    href = "/v1/accounts" },
        new { rel = "health",      href = "/health" },
        new { rel = "openapi",     href = "/openapi/v1.json" },
    },
})).WithTags("Root").WithOpenApi();

app.MapGet("/v1", () => Results.Ok(new
{
    service = "ApiPlatform.Api",
    version = "1.0",
    links = new[]
    {
        new { rel = "accounts", href = "/v1/accounts" },
    },
})).WithTags("Root").WithOpenApi();

// ── Account + Transaction endpoints ──────────────────────────────────────────
app.MapAccountEndpoints();

// ── Customer endpoints ────────────────────────────────────────────────────────
app.MapCustomerEndpoints();

// ── Webhook receiver + queue peek (anonymous) ─────────────────────────────────
app.MapWebhookEndpoints();

app.Run();

// Expose Program for WebApplicationFactory in tests
public partial class Program { }
