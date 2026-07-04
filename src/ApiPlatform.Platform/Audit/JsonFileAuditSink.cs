using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace ApiPlatform.Platform.Audit;

/// <summary>
/// Native-AOT-safe <see cref="IPlatformAudit"/> sink. It serializes the audit payload through
/// a caller-supplied source-generated <see cref="JsonSerializerOptions"/> (no reflection, no
/// Audit.NET), so it works in a trimmed/AOT host where the default Audit.NET sink does not.
/// Swap it in for AOT hosts: <c>services.AddSingleton&lt;IPlatformAudit&gt;(_ =&gt; new
/// JsonFileAuditSink(dir, MyJsonContext.Options))</c> after <c>AddPlatformCore</c>.
/// </summary>
public sealed class JsonFileAuditSink : IPlatformAudit
{
    private readonly string _directory;
    private readonly JsonSerializerOptions _options;
    private readonly TimeProvider _timeProvider;

    public JsonFileAuditSink(string directory, JsonSerializerOptions options, TimeProvider timeProvider)
    {
        _directory = directory;
        _options = options;
        _timeProvider = timeProvider;
        Directory.CreateDirectory(directory);
    }

    public async Task RecordAsync(string eventType, object data, CancellationToken ct = default)
    {
        // Resolve the payload's metadata from the source-gen resolver (AOT-safe — no reflection).
        JsonTypeInfo typeInfo = _options.GetTypeInfo(data.GetType());
        var dataJson = JsonSerializer.Serialize(data, typeInfo);

        var now = _timeProvider.GetUtcNow();
        // eventType is a platform-controlled identifier (no escaping needed); compose the envelope.
        var line = $"{{\"eventType\":\"{eventType}\",\"timestamp\":\"{now:O}\",\"data\":{dataJson}}}";
        var path = Path.Combine(_directory, $"audit-{now:yyyyMMdd}.json");
        await File.AppendAllTextAsync(path, line + Environment.NewLine, ct);
    }
}
