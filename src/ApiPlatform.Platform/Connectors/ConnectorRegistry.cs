using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Platform.Connectors;

/// <summary>
/// Scans assemblies for <see cref="IConnectorModule"/> implementations and registers them.
/// </summary>
public static class ConnectorRegistry
{
    /// <summary>
    /// Discovers all concrete, non-abstract <see cref="IConnectorModule"/> types in the given
    /// assemblies, instantiates each once, and calls <see cref="IConnectorModule.Register"/>.
    /// Each module type is registered at most once even if present in multiple assemblies.
    /// </summary>
    public static IServiceCollection AddConnectors(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] assemblies)
    {
        var registeredTypes = new HashSet<Type>();

        foreach (var assembly in assemblies)
        {
            var moduleTypes = assembly.GetTypes()
                .Where(t => typeof(IConnectorModule).IsAssignableFrom(t)
                            && !t.IsAbstract
                            && !t.IsInterface);

            foreach (var type in moduleTypes)
            {
                if (!registeredTypes.Add(type)) continue;

                var module = (IConnectorModule)Activator.CreateInstance(type)!;
                module.Register(services, configuration);
            }
        }

        return services;
    }
}
