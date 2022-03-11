using EventStore.Client;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StreamSubscription = Eventuous.EventStore.Subscriptions.StreamSubscription;

namespace Eventuous.Tests.EventStore;

public class RegistrationTests {
    ServiceProvider Provider { get; }

    const string SubId = "Test";

    static readonly StreamName Stream = new("teststream");

    public RegistrationTests() {
        var services = new ServiceCollection();

        services.AddSingleton(IntegrationFixture.Instance.Client);
        services.AddSingleton<ICheckpointStore, NoOpCheckpointStore>();

        services
            .AddSubscription<StreamSubscription, StreamSubscriptionOptions>(
                SubId,
                builder => builder
                    .Configure(x => x.StreamName = Stream)
                    .AddEventHandler<TestHandler>()
            );

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
        var pipe     = Sub.GetPrivateMember<ConsumePipe>("Pipe");
        var handlers = pipe!.GetHandlers();

        handlers.Should().HaveCount(1);
        handlers![0].Should().BeOfType<TracedEventHandler>();
        var innerHandler = handlers[0].GetPrivateMember<IEventHandler>("_innerHandler");
        innerHandler.Should().BeOfType(typeof(TestHandler));
    }

    [Fact]
    public void ShouldHaveProperId() => Sub.SubscriptionId.Should().Be(SubId);

    [Fact]
    public void ShouldHaveEventStoreClient() {
        var client = Sub.GetPrivateMember<EventStoreClient>("EventStoreClient");

        client.Should().Be(IntegrationFixture.Instance.Client);
    }

    [Fact]
    public void ShouldHaveNoOpStore() {
        var store = Sub.GetPrivateMember<ICheckpointStore>("CheckpointStore");

        store.Should().BeOfType<NoOpCheckpointStore>();
    }

    [Fact]
    public void ShouldResolveAsHostedService() {
        var services = Provider.GetServices<IHostedService>().ToArray();
        services.Length.Should().Be(1);

        var sub = services[0].GetPrivateMember<IMessageSubscription>("_subscription");
        sub.Should().Be(Sub);
    }
}

public class TestHandler : BaseEventHandler {
    public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext evt) => default;
}