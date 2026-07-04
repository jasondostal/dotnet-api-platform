using ApiPlatform.Integration.Eventing;
using ApiPlatform.Integration.Runtime;
using ApiPlatform.Platform.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApiPlatform.Tests;

/// <summary>
/// The eventing connector self-registers via AddIntegration and defaults to an in-memory
/// publisher when no Azure connection string is configured — proving offline eventing with
/// zero core wiring.
/// </summary>
public class EventingPublisherTests
{
    [Fact]
    public void Default_EventPublisher_IsInMemory_AndCapturesPublishedEvents()
    {
        var config = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddPlatformCore(config);
        services.AddIntegration(config);
        using var provider = services.BuildServiceProvider();

        var publisher = provider.GetRequiredService<IIntegrationEventPublisher>();
        Assert.IsType<InMemoryEventPublisher>(publisher);

        publisher.PublishAsync("Account.Touched", new { accountId = 42 }).GetAwaiter().GetResult();

        var captured = provider.GetRequiredService<InMemoryEventPublisher>().Published;
        Assert.Single(captured);
        Assert.Equal("Account.Touched", captured.First().EventType);
    }
}
