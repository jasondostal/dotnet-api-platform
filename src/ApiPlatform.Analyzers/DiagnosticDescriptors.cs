using Microsoft.CodeAnalysis;

namespace ApiPlatform.Analyzers;

/// <summary>Diagnostics emitted by the platform governance analyzers.</summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "ApiPlatform.Governance";

    /// <summary>APL0001 — a governed domain source was registered outside the connector registry.</summary>
    public static readonly DiagnosticDescriptor SourceRegisteredOutsideRegistry = new(
        id: "APL0001",
        title: "Domain source registered outside the connector registry",
        messageFormat: "'{0}' is a governed domain source and may only be registered inside an IConnectorModule.Register (discovered by AddConnectors) — registering it directly bypasses the audited/observed data path",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Interfaces that implement IGovernedSource (ApiPlatform.Platform.Connectors.IGovernedSource) must be wired through the connector registry so every data path inherits audit, PII masking, and tracing by construction. Registering one directly in a host or endpoint is a governance bypass.");

    /// <summary>APL0002 — an IConnectorModule implementation is not public and won't be discovered.</summary>
    public static readonly DiagnosticDescriptor ConnectorModuleNotPublic = new(
        id: "APL0002",
        title: "Connector module must be public",
        messageFormat: "Connector module '{0}' must be public so the connector registry can discover and instantiate it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "IConnectorModule implementations are discovered via assembly scan + Activator.CreateInstance, which requires a public type. A non-public module is silently never registered — a footgun this rule turns into a build error.");

    /// <summary>APL0003 — a direct ambient clock read from DateTime or DateTimeOffset.</summary>
    public static readonly DiagnosticDescriptor DateTimeAmbientClockRead = new(
        id: "APL0003",
        title: "Ambient clock read: use TimeProvider instead",
        messageFormat: "'{0}' reads the ambient system clock directly; inject TimeProvider and call GetUtcNow() so the clock is test-controllable",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "DateTime.Now/UtcNow and DateTimeOffset.Now/UtcNow are non-deterministic and cannot be controlled in unit tests. The platform standard is TimeProvider: inject it as a dependency, call GetUtcNow(), and register TimeProvider.System as a singleton in composition roots.");

    /// <summary>APL0004 — a direct write to System.Console instead of ILogger.</summary>
    public static readonly DiagnosticDescriptor ConsoleWrite = new(
        id: "APL0004",
        title: "Console write: use ILogger instead",
        messageFormat: "'{0}' writes directly to the console; use ILogger so output is structured, routed through the observability pipeline, and captured for audit",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Direct Console.Write*, Console.WriteLine, and reads of Console.Out/Error/In bypass the ILogger abstraction. All diagnostic output must flow through ILogger so it carries correlation IDs, respects configured log levels, and reaches the configured sinks — including the audit trail.");

    /// <summary>APL0005 — a problem response is missing a canonical RFC 9457 <c>type</c> URI.</summary>
    public static readonly DiagnosticDescriptor ProblemTypeMissing = new(
        id: "APL0005",
        title: "Problem response missing RFC 9457 type URI",
        messageFormat: "'{0}' omits the 'type' argument; every problem response must carry a canonical type URI so consumers can classify errors machine-side without string-matching titles",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "RFC 9457 requires a 'type' URI that uniquely identifies the error class. Omitting it forces API consumers to match on 'title' strings, which are unstable and may be internationalised. Pass a canonical URI from ProblemTypes as the 'type:' argument.");
}
