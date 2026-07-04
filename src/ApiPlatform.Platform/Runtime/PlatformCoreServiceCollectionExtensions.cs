using ApiPlatform.Platform.Audit;
using ApiPlatform.Platform.Pii;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Platform.Runtime;

/// <summary>
/// DI registration entry point for the platform governance core.
/// </summary>
public static class PlatformCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers core platform services: audit configuration, PII redaction, and audit recording.
    /// </summary>
    public static IServiceCollection AddPlatformCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        PlatformAudit.Configure(configuration);

        // Platform-standard clock abstraction; register once so all services get the same instance.
        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<IPiiRedactor, DefaultPiiRedactor>();
        services.AddSingleton<IPlatformAudit, AuditNetPlatformAudit>();

        // Default "who" — a system identity; web hosts override with an HTTP-backed context.
        services.AddSingleton<IAuditContext>(_ => new SystemAuditContext());

        return services;
    }
}
