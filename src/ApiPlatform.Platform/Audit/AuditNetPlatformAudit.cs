using Audit.Core;

namespace ApiPlatform.Platform.Audit;

/// <summary>
/// <see cref="IPlatformAudit"/> implementation backed by Audit.NET.
/// Uses the globally-configured data provider (set up by <see cref="PlatformAudit.Configure"/>).
/// </summary>
public sealed class AuditNetPlatformAudit : IPlatformAudit
{
    public Task RecordAsync(string eventType, object data, CancellationToken ct = default)
        => AuditScope.LogAsync(eventType, new { payload = data }, ct);
}
