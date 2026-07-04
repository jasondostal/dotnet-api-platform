namespace ApiPlatform.Api.Eventing;

/// <summary>
/// Publishes domain events to the configured event infrastructure.
/// Failures must propagate — implementations must NOT silently swallow exceptions —
/// so callers can decide whether to proceed with an operation or treat it as failed.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes a <c>northwind.account.touched</c> CloudEvent.
    /// Throws on publish failure; the caller is responsible for exception handling.
    /// </summary>
    Task PublishAccountTouchedAsync(Guid accountId, CancellationToken ct = default);
}
