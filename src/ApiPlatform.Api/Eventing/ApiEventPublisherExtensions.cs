using Microsoft.Extensions.Hosting;

namespace ApiPlatform.Api.Eventing;

/// <summary>
/// Registers the appropriate <see cref="IEventPublisher"/> based on the
/// <c>EVENT_PUBLISHER_TYPE</c> configuration key.
/// </summary>
public static class ApiEventPublisherExtensions
{
    /// <summary>
    /// Adds the configured event publisher to <paramref name="services"/>.
    ///
    /// <list type="bullet">
    ///   <item>
    ///     <c>InMemory</c> (default) — in-memory, offline-safe, no configuration required.
    ///     Also resolvable as its concrete type for test assertions.
    ///   </item>
    ///   <item>
    ///     <c>EventGrid</c> — publishes to Azure Event Grid; requires both
    ///     <c>EVENTGRID_TOPIC_ENDPOINT</c> and <c>EVENTGRID_TOPIC_KEY</c>.
    ///     In a non-Development environment, absent config is a startup error (fail-fast).
    ///     In Development the publisher silently falls back to in-memory.
    ///   </item>
    /// </list>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown at composition when <c>EVENT_PUBLISHER_TYPE=EventGrid</c> and the required
    /// config values are absent in a non-Development environment.
    /// </exception>
    public static IServiceCollection AddApiEventPublisher(
        this IServiceCollection services,
        IConfiguration          configuration,
        IHostEnvironment        environment)
    {
        var publisherType = configuration["EVENT_PUBLISHER_TYPE"] ?? "InMemory";

        if (publisherType.Equals("EventGrid", StringComparison.OrdinalIgnoreCase))
        {
            var endpoint = configuration["EVENTGRID_TOPIC_ENDPOINT"];
            var key      = configuration["EVENTGRID_TOPIC_KEY"];

            if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(key))
            {
                // Fully configured — wire the real EventGrid publisher.
                services.AddSingleton<IEventPublisher, EventGridEventPublisher>();
                return services;
            }

            // Config absent: fail-fast in non-Development; silently fall through in Development.
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "EVENT_PUBLISHER_TYPE=EventGrid requires EVENTGRID_TOPIC_ENDPOINT and " +
                    "EVENTGRID_TOPIC_KEY to be configured. " +
                    "Set EVENT_PUBLISHER_TYPE=InMemory for local development.");
            }

            // Development only: fall through to in-memory below.
        }

        // Default / fallback: in-memory publisher, also resolvable as concrete type.
        services.AddSingleton<InMemoryApiEventPublisher>();
        services.AddSingleton<IEventPublisher>(
            sp => sp.GetRequiredService<InMemoryApiEventPublisher>());
        return services;
    }
}
