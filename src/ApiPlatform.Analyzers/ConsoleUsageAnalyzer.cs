using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ApiPlatform.Analyzers;

/// <summary>
/// APL0004 — direct writes to <c>System.Console</c> (Write, WriteLine, or access to the
/// Out/Error/In stream properties) are forbidden in library and host code.  All diagnostic
/// output must flow through the <c>ILogger</c> abstraction so it is structured, routable,
/// and captured by the observability and audit pipeline.  A bare <c>Console.WriteLine</c>
/// bypasses correlation IDs, log levels, sinks, and any middleware that inspects or
/// redacts the output stream.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ConsoleUsageAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(DiagnosticDescriptors.ConsoleWrite);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
    }

    private static bool IsSystemConsole(ITypeSymbol? type) =>
        type is not null
        && type.Name == "Console"
        && type.ContainingNamespace?.ToDisplayString() == "System";

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (!method.Name.StartsWith("Write"))
            return;

        if (!IsSystemConsole(method.ContainingType))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ConsoleWrite,
            context.Operation.Syntax.GetLocation(),
            $"Console.{method.Name}"));
    }

    private static void AnalyzePropertyReference(OperationAnalysisContext context)
    {
        var propertyRef = (IPropertyReferenceOperation)context.Operation;
        var property = propertyRef.Property;

        if (property.Name != "Out" && property.Name != "Error" && property.Name != "In")
            return;

        if (!IsSystemConsole(property.ContainingType))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ConsoleWrite,
            context.Operation.Syntax.GetLocation(),
            $"Console.{property.Name}"));
    }
}
