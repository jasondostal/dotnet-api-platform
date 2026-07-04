namespace ApiPlatform.Platform.Audit;

/// <summary>
/// The canonical compliance audit record written for EVERY operation through a governed source —
/// read or write (list, get, create, renew, change-value, …). Who (Actor), what (Operation on
/// Resource, with masked Inputs), the Outcome, and the TraceId that joins this examiner-facing
/// record to the engineer-facing logs/traces of the same operation. A named type so it serializes
/// through the source-gen / AOT-safe sink.
/// </summary>
public sealed record AccessAuditRecord(
    string Actor,
    string Operation,
    string Resource,
    string? Inputs,
    string Outcome,
    string? TraceId,
    DateTimeOffset At);
