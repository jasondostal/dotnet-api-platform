; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/AnalyzerReleases.md

### New Rules

Rule ID | Category                | Severity | Notes
--------|-------------------------|----------|----------------------------------------------------
APL0001 | ApiPlatform.Governance  | Warning  | Domain source registered outside the connector registry
APL0002 | ApiPlatform.Governance  | Warning  | Connector module must be public
APL0003 | ApiPlatform.Governance  | Warning  | Ambient clock read via DateTime/DateTimeOffset: use TimeProvider
APL0004 | ApiPlatform.Governance  | Warning  | Console write: use ILogger instead
APL0005 | ApiPlatform.Governance  | Warning  | Problem response missing RFC 9457 type URI
