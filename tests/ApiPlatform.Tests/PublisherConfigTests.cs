using ApiPlatform.Api.Eventing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace ApiPlatform.Tests;

/// <summary>
/// Covers the three publisher-selection invariants for <see cref="IEventPublisher"/>:
///   1. Zero-config default resolves to <see cref="InMemoryApiEventPublisher"/>.
///   2. <c>EVENT_PUBLISHER_TYPE=EventGrid</c> in a non-Development environment without
///      cloud config throws <see cref="InvalidOperationException"/> at composition.
///   3. <c>EVENT_PUBLISHER_TYPE=EventGrid</c> in Development without cloud config
///      silently falls back to <see cref="InMemoryApiEventPublisher"/>.
/// </summary>
public class PublisherConfigTests
{
    // ── Minimal IHostEnvironment stub ──────────────────────────────────────────

    private sealed class StubEnvironment : IHostEnvironment
    {
        public StubEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string       EnvironmentName         { get; set; }
        public string       ApplicationName         { get; set; } = "test";
        public string       ContentRootPath         { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    // ── Test 1: zero-config default → InMemory ─────────────────────────────────

    [Fact]
    public void AddApiEventPublisher_NoConfig_DefaultsToInMemory()
    {
        var config   = new ConfigurationBuilder().Build();
        var env      = new StubEnvironment("Development");
        var services = new ServiceCollection();

        services.AddApiEventPublisher(config, env);

        using var sp = services.BuildServiceProvider();
        Assert.IsType<InMemoryApiEventPublisher>(sp.GetRequiredService<IEventPublisher>());
    }

    // ── Test 2: explicit InMemory → InMemory ──────────────────────────────────

    [Fact]
    public void AddApiEventPublisher_ExplicitInMemory_RegistersInMemory()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EVENT_PUBLISHER_TYPE"] = "InMemory",
            })
            .Build();
        var env      = new StubEnvironment("Production");
        var services = new ServiceCollection();

        services.AddApiEventPublisher(config, env);

        using var sp = services.BuildServiceProvider();
        Assert.IsType<InMemoryApiEventPublisher>(sp.GetRequiredService<IEventPublisher>());
    }

    // ── Test 3: EventGrid + non-Development + no cloud config → fail-fast ─────

    [Fact]
    public void AddApiEventPublisher_EventGrid_NonDev_NoCloudConfig_ThrowsAtComposition()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EVENT_PUBLISHER_TYPE"] = "EventGrid",
                // EVENTGRID_TOPIC_ENDPOINT and EVENTGRID_TOPIC_KEY intentionally absent
            })
            .Build();
        var env      = new StubEnvironment("Production");
        var services = new ServiceCollection();

        var ex = Assert.Throws<InvalidOperationException>(
            () => services.AddApiEventPublisher(config, env));

        Assert.Contains("EventGrid", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ── Test 4: EventGrid + Development + no cloud config → falls back to InMemory

    [Fact]
    public void AddApiEventPublisher_EventGrid_Dev_NoCloudConfig_FallsBackToInMemory()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EVENT_PUBLISHER_TYPE"] = "EventGrid",
                // No endpoint/key — tolerated in Development
            })
            .Build();
        var env      = new StubEnvironment("Development");
        var services = new ServiceCollection();

        // Should not throw in Development.
        services.AddApiEventPublisher(config, env);

        using var sp = services.BuildServiceProvider();
        Assert.IsType<InMemoryApiEventPublisher>(sp.GetRequiredService<IEventPublisher>());
    }

    // ── Test 5: InMemory publisher is also resolvable as its concrete type ────

    [Fact]
    public void AddApiEventPublisher_Default_InMemoryAlsoResolvableAsConcrete()
    {
        var config   = new ConfigurationBuilder().Build();
        var env      = new StubEnvironment("Development");
        var services = new ServiceCollection();

        services.AddApiEventPublisher(config, env);

        using var sp = services.BuildServiceProvider();
        Assert.NotNull(sp.GetRequiredService<InMemoryApiEventPublisher>());
    }
}
