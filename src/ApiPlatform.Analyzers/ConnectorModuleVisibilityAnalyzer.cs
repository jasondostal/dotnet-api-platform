using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ApiPlatform.Analyzers;

/// <summary>
/// APL0002 — a class implementing ApiPlatform.Platform.Connectors.IConnectorModule must be public.
/// The connector registry discovers modules via assembly scan + Activator.CreateInstance, which
/// requires a public type; a non-public module is silently never registered. This rule turns that
/// invisible footgun into a build error.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConnectorModuleVisibilityAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(DiagnosticDescriptors.ConnectorModuleNotPublic);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class || type.IsAbstract)
            return;

        var implementsModule = type.AllInterfaces.Any(i =>
            i.Name == "IConnectorModule"
            && i.ContainingNamespace?.ToDisplayString() == "ApiPlatform.Platform.Connectors");
        if (!implementsModule)
            return;

        if (type.DeclaredAccessibility == Accessibility.Public)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ConnectorModuleNotPublic,
            type.Locations.FirstOrDefault(),
            type.Name));
    }
}
