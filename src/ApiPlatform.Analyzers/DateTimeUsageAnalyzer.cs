using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ApiPlatform.Analyzers;

/// <summary>
/// APL0003 — reads of <c>DateTime.Now</c>, <c>DateTime.UtcNow</c>, <c>DateTimeOffset.Now</c>,
/// or <c>DateTimeOffset.UtcNow</c> are non-deterministic and untestable. The platform standard is
/// <c>TimeProvider</c>: inject it as a dependency, call <c>GetUtcNow()</c>, and register
/// <c>TimeProvider.System</c> as a singleton in composition roots. Any type that reads the wall
/// clock through these properties cannot be deterministically tested without time-skewing hacks.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DateTimeUsageAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(DiagnosticDescriptors.DateTimeAmbientClockRead);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
    }

    private static void AnalyzePropertyReference(OperationAnalysisContext context)
    {
        var propertyRef = (IPropertyReferenceOperation)context.Operation;
        var property = propertyRef.Property;

        if (property.Name != "Now" && property.Name != "UtcNow")
            return;

        var containingType = property.ContainingType;
        if (containingType is null)
            return;

        var ns = containingType.ContainingNamespace?.ToDisplayString();
        if (ns != "System")
            return;

        if (containingType.Name != "DateTime" && containingType.Name != "DateTimeOffset")
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.DateTimeAmbientClockRead,
            context.Operation.Syntax.GetLocation(),
            $"{containingType.Name}.{property.Name}"));
    }
}
