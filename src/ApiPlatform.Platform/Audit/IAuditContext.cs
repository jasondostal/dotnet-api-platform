namespace ApiPlatform.Platform.Audit;

/// <summary>
/// The "who" for an audit record — the authenticated actor and the scopes they presented for
/// the current operation. Web hosts populate it from the request principal; off-path hosts use
/// a system identity. Read at the source seam so every audit record can name the caller.
/// </summary>
public interface IAuditContext
{
    string Actor { get; }

    IReadOnlyCollection<string> Scopes { get; }
}
