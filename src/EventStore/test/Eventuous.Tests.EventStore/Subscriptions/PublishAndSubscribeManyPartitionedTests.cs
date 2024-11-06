using Eventuous.Producers;
using Eventuous.Tests.EventStore.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.EventStore.Subscriptions;

public class PublishAndSubscribeManyPartitionedTests() : LegacySubscriptionFixture(
        5.Milliseconds(),
        false,
        new StreamName(Guid.NewGuid().ToString("N")),
        logLevel: LogLevel.Trace
    ) {
    [Test]
    [Category("Stream catch-up subscription")]
    public async Task SubscribeAndProduceMany(CancellationToken cancellationToken) {
        const int count = 10;

        var testEvents = Enumerable.Range(1, count)
            .Select(i => new TestEvent(Auto.Create<string>(), i))
            .ToList();

        await Start();
        await Producer.Produce(Stream, testEvents, new Metadata(), cancellationToken: cancellationToken);
        await Handler.AssertCollection(5.Seconds(), [..testEvents]).Validate(cancellationToken);
        await Stop();

        CheckpointStore.GetCheckpoint(Subscription.SubscriptionId).Should().Be(count - 1);
    }
}
