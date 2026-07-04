using ApiPlatform.Platform.Audit;
using ApiPlatform.Platform.Connectors;
using ApiPlatform.Platform.Pii;
using Castle.DynamicProxy;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Integration.Acl.Governance;

/// <summary>
/// Wraps every vendor seam registration in a DynamicProxy carrying the single
/// <see cref="AuditInterceptor"/>, so audit + tracing apply to EVERY operation on EVERY source —
/// reads and writes — by construction. There is no per-interface or per-vendor code: any interface
/// that extends <see cref="IGovernedSource"/> (registered by any connector module) is governed
/// automatically, wherever it is declared. Add the 47th vendor's source with a
/// <c>CreateMemberAsync</c> write and it is audited the moment it is registered — the developer
/// writes zero audit code.
/// </summary>
public static class SourceGovernanceRegistration
{
    private static readonly ProxyGenerator ProxyGenerator = new();

    /// <summary>
    /// Decorates every registered seam interface with the governance proxy. Call once, after all
    /// connector modules + the routing aggregator have registered their sources.
    /// </summary>
    public static IServiceCollection GovernSources(this IServiceCollection services)
    {
        for (var i = 0; i < services.Count; i++)
        {
            var descriptor = services[i];
            if (!IsGovernedSeam(descriptor.ServiceType))
                continue;

            var captured = descriptor;
            services[i] = ServiceDescriptor.Describe(
                descriptor.ServiceType,
                sp => CreateGovernedProxy(descriptor.ServiceType, InstantiateInner(captured, sp), sp),
                descriptor.Lifetime);
        }

        return services;
    }

    // The seam is every interface that extends IGovernedSource — the explicit marker that declares
    // a contract as canonical and governed. This is a type relationship, not a namespace string, so
    // a seam is governed wherever it is declared (no namespace-escape hole). The marker itself is
    // excluded to avoid an infinite proxy chain.
    private static bool IsGovernedSeam(Type type) =>
        type.IsInterface && type != typeof(IGovernedSource) && typeof(IGovernedSource).IsAssignableFrom(type);

    private static object CreateGovernedProxy(Type seam, object target, IServiceProvider sp)
    {
        var interceptor = new AuditInterceptor(
            sp.GetRequiredService<IPlatformAudit>(),
            sp.GetRequiredService<IAuditContext>(),
            sp.GetRequiredService<IPiiRedactor>(),
            sp.GetRequiredService<TimeProvider>()).ToInterceptor();

        return ProxyGenerator.CreateInterfaceProxyWithTargetInterface(seam, target, interceptor);
    }

    private static object InstantiateInner(ServiceDescriptor descriptor, IServiceProvider sp)
    {
        if (descriptor.ImplementationInstance is not null)
            return descriptor.ImplementationInstance;
        if (descriptor.ImplementationFactory is not null)
            return descriptor.ImplementationFactory(sp);
        return ActivatorUtilities.CreateInstance(sp, descriptor.ImplementationType!);
    }
}
