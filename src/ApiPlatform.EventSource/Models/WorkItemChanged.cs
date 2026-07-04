namespace ApiPlatform.EventSource.Models;

/// <summary>
/// Domain event that signals a work-item state transition.
/// </summary>
public sealed record WorkItemChanged
{
    public Guid           WorkItemId { get; init; }
    public string         ChangeType { get; init; } = string.Empty;
    public DateTimeOffset At         { get; init; }
}
