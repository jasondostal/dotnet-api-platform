using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ApiPlatform.Analyzers;

/// <summary>
/// APL0005 — every <c>Results.Problem()</c> / <c>TypedResults.Problem()</c> call on the
/// string-argument overload must supply an explicit <c>type</c> URI so consumers can classify
/// errors machine-side (RFC 9457). Omitting <c>type</c> forces consumers to string-match
/// <c>title</c>, which is unstable and internationalised.
/// <para>
/// Exempt: the <c>Problem(ProblemDetails)</c> single-object overload — its <c>type</c> is
/// set on the object itself.
/// </para>
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ProblemTypeAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(DiagnosticDescriptors.ProblemTypeMissing);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;

        if (method.Name != "Problem")
            return;

        var containingType = method.ContainingType;
        if (containingType is null)
            return;

        if (containingType.Name != "Results" && containingType.Name != "TypedResults")
            return;

        var ns = containingType.ContainingNamespace?.ToDisplayString();
        if (ns != "Microsoft.AspNetCore.Http")
            return;

        // Find the `type` parameter in the argument list. If there is no `type` parameter
        // (e.g. the single-ProblemDetails overload), skip — the exempt case.
        var typeArg = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == "type");
        if (typeArg is null)
            return;

        // Fire only when the caller omitted the `type` argument (left at default null).
        if (typeArg.ArgumentKind != ArgumentKind.DefaultValue)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.ProblemTypeMissing,
            invocation.Syntax.GetLocation(),
            $"{containingType.Name}.Problem"));
    }
}
