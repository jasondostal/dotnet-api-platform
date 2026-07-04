namespace ApiPlatform.Platform.Audit;

/// <summary>
/// Records structured audit events to the configured durable store.
/// </summary>
public interface IPlatformAudit
{
    Task RecordAsync(string eventType, object data, CancellationToken ct = default);
}
