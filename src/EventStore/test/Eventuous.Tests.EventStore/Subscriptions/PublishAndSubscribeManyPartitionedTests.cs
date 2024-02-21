using Eventuous.Producers;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.EventStore.Subscriptions;

[Collection("Database")]
public class PublishAndSubscribeManyPartitionedTests(ITestOutputHelper output)
    : LegacySubscriptionFixture<TestEventHandler>(
        output,
        new TestEventHandler(5.Milliseconds()),
        false,
        new StreamName(Guid.NewGuid().ToString("N")),
        logLevel: LogLevel.Trace
    ) {
    [Fact]
    public async Task SubscribeAndProduceMany() {
        const int count = 10;

        var testEvents = Enumerable.Range(1, count)
            .Select(i => new TestEvent(Auto.Create<string>(), i))
            .ToList();

        await Start();
        await Producer.Produce(Stream, testEvents, new Metadata());
        await Handler.AssertCollection(5.Seconds(), [..testEvents]).Validate();
        await Stop();

        CheckpointStore.GetCheckpoint(Subscription.SubscriptionId).Should().Be(count - 1);
    }
}
