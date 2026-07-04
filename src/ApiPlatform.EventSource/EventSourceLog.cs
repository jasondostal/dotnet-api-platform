using Microsoft.Extensions.Logging;

namespace ApiPlatform.EventSource;

/// <summary>
/// Source-generated log messages for the change-feed emitter (see <c>[LoggerMessage]</c>):
/// compile-time, allocation-free, with the template checked against its arguments.
/// </summary>
internal static partial class EventSourceLog
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Debug,
        Message = "Emitted {ChangeType} event for WorkItem {WorkItemId}")]
    public static partial void EmittedChange(ILogger logger, string changeType, Guid workItemId);
}
