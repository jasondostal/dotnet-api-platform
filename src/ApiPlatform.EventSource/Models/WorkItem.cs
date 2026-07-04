namespace ApiPlatform.EventSource.Models;

/// <summary>
/// Minimal work-item representation local to this host; not a Contracts type.
/// </summary>
public sealed record WorkItem
{
    public Guid   Id     { get; init; }
    public string Title  { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}
