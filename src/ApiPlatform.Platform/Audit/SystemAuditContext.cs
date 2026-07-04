namespace ApiPlatform.Platform.Audit;

/// <summary>
/// Default <see cref="IAuditContext"/> for off-path hosts (Poller, EventSource, MCP) that act
/// as the platform itself rather than on behalf of a request principal. Web hosts override this
/// with an HTTP-backed context.
/// </summary>
public sealed class SystemAuditContext : IAuditContext
{
    public SystemAuditContext(string actor = "system") => Actor = actor;

    public string Actor { get; }

    public IReadOnlyCollection<string> Scopes { get; } = [];
}
