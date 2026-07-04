using System.Text.Json;
using System.Text.Json.Serialization;

namespace ApiPlatform.Poller;

/// <summary>Named, source-gen-serializable audit payload for a seen record (AOT-safe).</summary>
public sealed record RecordCreatedAudit(Guid Id, string MaskedEmail, DateTimeOffset CreatedAt);

/// <summary>
/// System.Text.Json source-generation context for the poller's audit payloads. Provides the
/// reflection-free metadata the AOT audit sink uses, so the native image serializes audit
/// records with no runtime reflection.
/// </summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RecordCreatedAudit))]
[JsonSerializable(typeof(CursorState))]
public sealed partial class PollerJsonContext : JsonSerializerContext
{
}

/// <summary>Shared serializer options bound to the source-gen resolver.</summary>
public static class PollerJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = PollerJsonContext.Default,
    };
}
