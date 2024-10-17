using EventStore.Client;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Producers;
using Eventuous.Subscriptions.Filters;
using Eventuous.Tests.EventStore.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using static Xunit.TestContext;

namespace Eventuous.Tests.EventStore.Subscriptions;

[Collection("Database")]
public class StreamPersistentPublishAndSubscribeManyTests1(ITestOutputHelper outputHelper)
    : PersistentSubscriptionFixture<StreamPersistentSubscription, StreamPersistentSubscriptionOptions, TestEventHandler>(outputHelper, new(), false) {
    [Fact]
    [Trait("Category", "Persistent subscription")]
    public async Task SubscribeAndProduceMany() {
        const int count = 1000;

        var testEvents = Auto.CreateMany<TestEvent>(count).ToList();

        await Start();
        await Producer.Produce(Stream, testEvents, new(), cancellationToken: Current.CancellationToken);
        await Handler.AssertCollection(10.Seconds(), [..testEvents]).Validate(Current.CancellationToken);
        await Stop();
    }

    protected override StreamPersistentSubscription CreateSubscription(string id, ILoggerFactory loggerFactory)
        => new(
            Fixture.Client,
            new() {
                StreamName     = Stream,
                SubscriptionId = id
            },
            new ConsumePipe().AddDefaultConsumer(Handler),
            loggerFactory
        );
}

[Collection("Database")]
public class StreamPersistentPublishAndSubscribeManyTests2(ITestOutputHelper outputHelper)
    : PersistentSubscriptionFixture<StreamPersistentSubscription, StreamPersistentSubscriptionOptions, TestEventHandler>(outputHelper, new(), false) {
    [Fact]
    [Trait("Category", "Persistent subscription")]
    public async Task SubscribeAndProduceMany() {
        const int count = 1000;

        var testEvents = Auto.CreateMany<TestEvent>(count).ToList();

        await Start();
        await Producer.Produce(Stream, testEvents, new(), cancellationToken: Current.CancellationToken);
        await Handler.AssertCollection(10.Seconds(), [..testEvents]).Validate(Current.CancellationToken);
        await Stop();
    }

    protected override StreamPersistentSubscription CreateSubscription(string id, ILoggerFactory loggerFactory) {
        var connectionString = Fixture.Container.GetConnectionString();
        var settings         = EventStoreClientSettings.Create(connectionString);
        var client           = new EventStorePersistentSubscriptionsClient(settings);
        return new(
            client,
            new() {
                StreamName     = Stream,
                SubscriptionId = id
            },
            new ConsumePipe().AddDefaultConsumer(Handler),
            loggerFactory
        );
    }
}
