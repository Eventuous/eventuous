using EventStore.Client;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Producers;
using Eventuous.Subscriptions.Filters;
using Eventuous.Tests.EventStore.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.EventStore.Subscriptions;

public class StreamPersistentPublishAndSubscribeManyTests {
    [Test]
    [Category("Persistent subscription")]
    [MethodDataSource(nameof(GetFixtures))]
    public async Task SubscribeAndProduceMany(
            PersistentSubscriptionFixture<StreamPersistentSubscription, StreamPersistentSubscriptionOptions, TestEventHandler> fixture,
            CancellationToken                                                                                                  cancellationToken
        ) {
        const int count = 1000;

        var testEvents = TestEvent.CreateMany(count);

        await fixture.InitializeAsync();
        await fixture.Start();
        await fixture.Producer.Produce(fixture.Stream, testEvents, new(), cancellationToken: cancellationToken);
        await fixture.Handler.AssertCollection(10.Seconds(), [..testEvents]).Validate(cancellationToken);
        await fixture.Stop();
        await fixture.DisposeAsync();
    }

    public static IEnumerable<Func<PersistentSubscriptionFixture<StreamPersistentSubscription, StreamPersistentSubscriptionOptions, TestEventHandler>>> GetFixtures() {
        yield return () => new(new(), CreateWithRegularClient, false);
        yield return () => new(new(), CreateWithPersistentSubClient, false);
    }

    static StreamPersistentSubscription CreateWithRegularClient(string id, string connectionString, StreamName stream, TestEventHandler handler, ILoggerFactory loggerFactory) {
        var settings = EventStoreClientSettings.Create(connectionString);

        return new(
            new EventStoreClient(settings),
            new() {
                StreamName     = stream,
                SubscriptionId = id
            },
            new ConsumePipe().AddDefaultConsumer(handler),
            loggerFactory
        );
    }

    static StreamPersistentSubscription CreateWithPersistentSubClient(string id, string connectionString, StreamName stream, TestEventHandler handler, ILoggerFactory loggerFactory) {
        var settings = EventStoreClientSettings.Create(connectionString);
        var client   = new EventStorePersistentSubscriptionsClient(settings);

        return new(
            client,
            new() {
                StreamName     = stream,
                SubscriptionId = id
            },
            new ConsumePipe().AddDefaultConsumer(handler),
            loggerFactory
        );
    }
}
