using System.Reflection;
using EventStore.Client;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Context;
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
        var handlers = GetPrivateMember<IEnumerable<IEventHandler>>(Sub, "EventHandlers")?.ToArray();

        handlers.Should().HaveCount(1);
        handlers.Should().ContainSingle(x => x.GetType() == typeof(TestHandler));
    }

    [Fact]
    public void ShouldHaveProperId() => Sub.ServiceId.Should().Be(SubId);

    [Fact]
    public void ShouldHaveEventStoreClient() {
        var client = GetPrivateMember<EventStoreClient>(Sub, "EventStoreClient");
        client.Should().Be(IntegrationFixture.Instance.Client);
    }

    [Fact]
    public void ShouldHaveNoOpStore() {
        var store = GetPrivateMember<ICheckpointStore>(Sub, "CheckpointStore");

        store.Should().BeOfType<NoOpCheckpointStore>();
    }

    [Fact]
    public void ShouldResolveAsHostedService() {
        var service = Provider.GetService<IHostedService>();
        service.Should().Be(Sub);
    }

    static TMember? GetPrivateMember<TMember>(object instance, string name) where TMember : class
        => GetMember<TMember>(instance.GetType(), instance, name);

    static TMember? GetMember<TMember>(Type instanceType, object instance, string name) where TMember : class {
        const BindingFlags flags = BindingFlags.Instance
                                 | BindingFlags.Public
                                 | BindingFlags.NonPublic
                                 | BindingFlags.Static;

        var field  = instanceType.GetField(name, flags);
        var prop   = instanceType.GetProperty(name, flags);
        var member = prop?.GetValue(instance) ?? field?.GetValue(instance);

        return member == null && instanceType.BaseType != null
            ? GetMember<TMember>(instanceType.BaseType, instance, name) : member as TMember;
    }
}

public class TestHandler : IEventHandler {
    public Task HandleEvent(IMessageConsumeContext evt, CancellationToken cancellationToken) {
        return Task.CompletedTask;
    }
}