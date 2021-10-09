using System.Reflection;
using EventStore.Client;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Tests.EventStore.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StreamSubscription = Eventuous.EventStore.Subscriptions.StreamSubscription;

namespace Eventuous.Tests.EventStore;

public class RegistrationTests {
    ServiceProvider Provider { get; }

    const string SubId  = "Test";
    const string Stream = "teststream";

    public RegistrationTests() {
        var services = new ServiceCollection();

        services.AddSingleton(IntegrationFixture.Instance.Client);
        services.AddSingleton<ICheckpointStore, NoOpCheckpointStore>();

        services
            .AddSubscription<StreamSubscription, StreamSubscriptionOptions>(SubId, x => x.StreamName = Stream)
            .AddEventHandler<TestHandler>();

        Provider = services.BuildServiceProvider();

        Sub = Provider.GetService<StreamSubscription>()!;
    }

    StreamSubscription Sub { get; }

    [Fact]
    public void ShouldResolveSubscription() {
        Sub.Should().NotBeNull();
        Sub.Should().BeOfType<StreamSubscription>();
    }

    [Fact]
    public void ShouldHaveTestHandler() {
        var handlers =
            GetPrivateMember<SubscriptionService<StreamSubscriptionOptions>, IEnumerable<IEventHandler>>(
                    Sub,
                    "_eventHandlers"
                )
                ?.ToArray();

        handlers.Should().HaveCount(1);
        handlers.Should().ContainSingle(x => x.GetType() == typeof(TestHandler));
    }

    [Fact]
    public void ShouldHaveProperId() => Sub.ServiceId.Should().Be(SubId);

    [Fact]
    public void ShouldHaveEventStoreClient() {
        var client = GetPrivateMember<StreamSubscription, EventStoreClient>(Sub, "EventStoreClient");
        client.Should().Be(IntegrationFixture.Instance.Client);
    }

    [Fact]
    public void ShouldHaveNoOpStore() {
        var store = GetPrivateMember<SubscriptionService<StreamSubscriptionOptions>, ICheckpointStore>(
            Sub,
            "_checkpointStore"
        );

        store.Should().BeOfType<NoOpCheckpointStore>();
    }

    [Fact]
    public void ShouldResolveAsHostedService() {
        var service = Provider.GetService<IHostedService>();
        service.Should().Be(Sub);
    }

    static TMember? GetPrivateMember<TInstance, TMember>(object instance, string name)
        where TInstance : class where TMember : class {
        const BindingFlags flags = BindingFlags.Instance
                                 | BindingFlags.Public
                                 | BindingFlags.NonPublic
                                 | BindingFlags.Static;

        var field  = typeof(TInstance).GetField(name, flags);
        var prop   = typeof(TInstance).GetProperty(name, flags);
        var member = prop?.GetValue(instance) ?? field?.GetValue(instance);

        return member as TMember;
    }
}

public class TestHandler : IEventHandler {
    public Task HandleEvent(ReceivedEvent evt, CancellationToken cancellationToken) {
        return Task.CompletedTask;
    }
}