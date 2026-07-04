using ApiPlatform.Platform.Errors;
using ApiPlatform.Platform.Pii;
using Azure.Messaging;
using Azure.Storage.Queues;

namespace ApiPlatform.Api.Eventing;

/// <summary>
/// Webhook receiver + queue-peek endpoints. All routes are anonymous —
/// Event Grid cannot supply our authorization header.
/// </summary>
public static class WebhookEndpoints
{
    public static IEndpointRouteBuilder MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        // ── Event Grid abuse-protection handshake ────────────────────────────
        app.MapMethods("/hooks/events", ["OPTIONS"], HandleOptions)
            .WithTags("Webhooks")
            .WithOpenApi()
            .AllowAnonymous();

        // ── Incoming CloudEvents from Event Grid ─────────────────────────────
        app.MapPost("/hooks/events", HandleIncoming)
            .WithTags("Webhooks")
            .WithOpenApi()
            .AllowAnonymous();

        // ── View the received-event log ───────────────────────────────────────
        app.MapGet("/hooks/events/log", HandleLog)
            .WithTags("Webhooks")
            .WithOpenApi()
            .AllowAnonymous();

        // ── Queue peek ────────────────────────────────────────────────────────
        app.MapGet("/hooks/queues", HandleQueuePeek)
            .WithTags("Webhooks")
            .WithOpenApi()
            .AllowAnonymous();

        return app;
    }

    // ── OPTIONS — Event Grid abuse-protection handshake ──────────────────────

    private static IResult HandleOptions(HttpRequest request)
    {
        var origin = request.Headers["WebHook-Request-Origin"].ToString();
        if (string.IsNullOrEmpty(origin))
            return Results.Ok();

        return new WebhookHandshakeResult();
    }

    // ── POST — receive CloudEvent(s) ─────────────────────────────────────────

    private static async Task<IResult> HandleIncoming(
        HttpRequest        request,
        ReceivedEventLog   log,
        IConfiguration     config,
        ILogger<Program>   logger,
        IPiiRedactor       redactor,
        TimeProvider       timeProvider)
    {
        // Optional query-key guard
        var secret = config["WEBHOOK_SECRET"];
        if (!string.IsNullOrWhiteSpace(secret))
        {
            var provided = request.Query["key"].ToString();
            if (provided != secret)
                return Results.Problem(
                    title: ProblemTypes.Unauthorized.Title,
                    statusCode: ProblemTypes.Unauthorized.Status,
                    type: ProblemTypes.Unauthorized.Type);
        }

        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync();

        CloudEvent[] events;
        try
        {
            events = CloudEvent.ParseMany(new BinaryData(body)).ToArray();
        }
        catch
        {
            // Try single
            try
            {
                var single = CloudEvent.Parse(new BinaryData(body));
                events = single is not null ? [single] : [];
            }
            catch { events = []; }
        }

        foreach (var evt in events)
        {
            log.Append(new ReceivedEventEntry(
                Type:       evt.Type ?? "unknown",
                Id:         evt.Subject,
                ReceivedAt: timeProvider.GetUtcNow()));

            logger.LogInformation("Webhook received CloudEvent type={Type} subject={Subject}", evt.Type, evt.Subject);

            // The CloudEvent subject carries a raw domain identifier (e.g. an account id set
            // by the publisher) — mask it before it reaches the audit store. The event type and
            // the event's own correlation id stay legible for audit dedup/traceability.
            Audit.Core.AuditScope.Log("Webhook:CloudEventReceived", new
            {
                cloudEventType    = evt.Type,
                cloudEventSubject = redactor.Mask(evt.Subject),
                cloudEventId      = evt.Id,
            });
        }

        return Results.Ok(new { received = events.Length, total = log.TotalReceived });
    }

    // ── GET /hooks/events/log ─────────────────────────────────────────────────

    private static IResult HandleLog(ReceivedEventLog log)
        => Results.Ok(log.GetAll());

    // ── GET /hooks/queues — fan-out queue peek ────────────────────────────────

    private static async Task<IResult> HandleQueuePeek(IConfiguration config)
    {
        var connStr    = config["EVENTS_STORAGE_CONNECTION"];
        var namesRaw   = config["EVENTS_QUEUE_NAMES"];

        if (string.IsNullOrWhiteSpace(connStr) || string.IsNullOrWhiteSpace(namesRaw))
        {
            return Results.Ok(new { status = "not configured", queues = Array.Empty<object>() });
        }

        var queueNames = namesRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var results    = new List<object>();

        foreach (var name in queueNames)
        {
            try
            {
                var queueClient = new QueueClient(connStr, name);
                var props       = await queueClient.GetPropertiesAsync();
                var peeked      = await queueClient.PeekMessagesAsync(maxMessages: 5);

                results.Add(new
                {
                    name,
                    approximateMessageCount = props.Value.ApproximateMessagesCount,
                    peeked = peeked.Value.Select(m => m.Body.ToString()).ToArray(),
                });
            }
            catch (Exception ex)
            {
                results.Add(new { name, error = ex.Message });
            }
        }

        return Results.Ok(new { queues = results });
    }
}

/// <summary>
/// Minimal IResult that writes 200 + the required Event Grid handshake headers.
/// </summary>
internal sealed class WebhookHandshakeResult : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode  = 200;
        httpContext.Response.Headers["WebHook-Allowed-Origin"] = "*";
        httpContext.Response.Headers["WebHook-Allowed-Rate"]   = "*";
        return Task.CompletedTask;
    }
}
