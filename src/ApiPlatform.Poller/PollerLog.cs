using Microsoft.Extensions.Logging;

namespace ApiPlatform.Poller;

/// <summary>
/// Source-generated, allocation-free log messages for the poller. The <c>[LoggerMessage]</c>
/// generator emits the strongly-typed logging code at compile time: no boxing, no runtime
/// template parsing, and the message template is checked against its arguments by the compiler.
/// </summary>
internal static partial class PollerLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Processed record {RecordId} created at {CreatedAt} (email masked)")]
    public static partial void ProcessedRecord(ILogger logger, Guid recordId, DateTimeOffset createdAt);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Debug,
        Message = "Poll cycle advanced cursor to {Cursor}")]
    public static partial void CursorAdvanced(ILogger logger, DateTimeOffset cursor);
}
