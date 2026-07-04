using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace ApiPlatform.Analyzers;

/// <summary>
/// APL0001 — a governed domain source (any interface implementing
/// <c>ApiPlatform.Platform.Connectors.IGovernedSource</c>) may only be registered in the DI
/// container from inside an IConnectorModule.Register (which the connector registry discovers
/// and the governance decorators wrap) or the sanctioned registry/runtime extensions. Registering
/// one directly in a host Program or endpoint opens an un-audited data path — the exact thing the
/// platform forbids — so it fails the build.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SourceRegistrationAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(DiagnosticDescriptors.SourceRegisteredOutsideRegistry);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!IsServiceRegistration(invocation.TargetMethod))
            return;

        var governed = CollectServiceTypes(invocation).FirstOrDefault(IsGovernedSourceInterface);
        if (governed is null)
            return;

        if (IsSanctionedRegistrar(context.ContainingSymbol))
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.SourceRegisteredOutsideRegistry,
            invocation.Syntax.GetLocation(),
            governed.Name));
    }

    private static bool IsServiceRegistration(IMethodSymbol method)
    {
        if (!method.Name.StartsWith("Add") && !method.Name.StartsWith("TryAdd"))
            return false;
        var ns = method.ContainingType?.ContainingNamespace?.ToDisplayString();
        return ns != null && ns.StartsWith("Microsoft.Extensions.DependencyInjection");
    }

    private static ImmutableArray<ITypeSymbol> CollectServiceTypes(IInvocationOperation invocation)
    {
        var builder = ImmutableArray.CreateBuilder<ITypeSymbol>();
        foreach (var t in invocation.TargetMethod.TypeArguments)
            builder.Add(t);
        foreach (var arg in invocation.Arguments)
        {
            if (arg.Value is ITypeOfOperation typeOf)
                builder.Add(typeOf.TypeOperand);
        }
        return builder.ToImmutable();
    }

    // The seam is every interface that implements IGovernedSource — the explicit marker declaring
    // a contract as canonical and governed. Governance is a type relationship, not a namespace
    // string, so seams declared in any namespace are caught. All governed interfaces must be
    // registered through the connector registry, never directly in a host or endpoint.
    private static bool IsGovernedSourceInterface(ITypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Interface)
            return false;
        return type.AllInterfaces.Any(i =>
            i.Name == "IGovernedSource" &&
            i.ContainingNamespace?.ToDisplayString() == "ApiPlatform.Platform.Connectors");
    }

    // Allowed: inside any IConnectorModule, or the registry / integration-runtime wiring points.
    private static bool IsSanctionedRegistrar(ISymbol? containingSymbol)
    {
        var type = containingSymbol as INamedTypeSymbol ?? containingSymbol?.ContainingType;
        while (type is not null)
        {
            if (type.AllInterfaces.Any(i => i.Name == "IConnectorModule"
                    && i.ContainingNamespace?.ToDisplayString() == "ApiPlatform.Platform.Connectors"))
                return true;

            if (type.Name is "ConnectorRegistry" or "IntegrationServiceCollectionExtensions")
                return true;

            type = type.ContainingType;
        }
        return false;
    }
}
