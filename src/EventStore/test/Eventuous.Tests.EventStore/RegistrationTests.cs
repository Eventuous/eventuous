using EventStore.Client;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using StreamSubscription = Eventuous.EventStore.Subscriptions.StreamSubscription;

namespace Eventuous.Tests.EventStore;

[ClassDataSource<StoreFixture>]
public class RegistrationTests(StoreFixture fixture) {
    const string SubId = "Test";

    static readonly StreamName Stream = new("teststream");

    ServiceProvider    Provider { get; set; } = null!;
    StreamSubscription Sub      { get; set; } = null!;

    [Test]
    [Category("Dependency injection")]
    public void ShouldResolveSubscription() {
        Sub.Should().NotBeNull();
        Sub.Should().BeOfType<StreamSubscription>();
    }

    [Test]
    [Category("Dependency injection")]
    public void ShouldHaveProperId() => Sub.SubscriptionId.Should().Be(SubId);

    [Test]
    [Category("Dependency injection")]
    public void ShouldHaveEventStoreClient() {
        var client = Sub.GetPrivateMember<EventStoreClient>("EventStoreClient");

        client.Should().Be(fixture.Client);
    }

    [Test]
    [Category("Dependency injection")]
    public void ShouldHaveNoOpStore() {
        var store = Sub.GetPrivateMember<ICheckpointStore>("CheckpointStore");

        store.Should().BeOfType<NoOpCheckpointStore>();
    }

    [Before(Test)]
    public ValueTask InitializeAsync() {
        var services = new ServiceCollection();

        services.AddSingleton(fixture.Client);
        services.AddSingleton<ICheckpointStore, NoOpCheckpointStore>();

        services
            .AddSubscription<StreamSubscription, StreamSubscriptionOptions>(
                SubId,
                builder => builder
                    .Configure(x => x.StreamName = Stream)
                    .AddEventHandler<TestHandler>()
            );

        Provider = services.BuildServiceProvider();
        Sub      = Provider.GetService<StreamSubscription>()!;

        return ValueTask.CompletedTask;
    }

    [After(Test)]
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public class TestHandler : BaseEventHandler {
    public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext evt)
        => ValueTask.FromResult(EventHandlingStatus.Success);
}
