using Eventuous.Producers;
using Eventuous.Tests.EventStore.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using static Xunit.TestContext;

namespace Eventuous.Tests.EventStore.Subscriptions;

[Collection("Database")]
public class PublishAndSubscribeManyPartitionedTests(ITestOutputHelper output)
    : LegacySubscriptionFixture<TestEventHandler>(
        output,
        new(new(5.Milliseconds(), output)),
        false,
        new StreamName(Guid.NewGuid().ToString("N")),
        logLevel: LogLevel.Trace
    ) {
    [Fact]
    [Trait("Category", "Stream catch-up subscription")]
    public async Task SubscribeAndProduceMany() {
        const int count = 10;

        var testEvents = Enumerable.Range(1, count)
            .Select(i => new TestEvent(Auto.Create<string>(), i))
            .ToList();

        await Start();
        await Producer.Produce(Stream, testEvents, new Metadata(), cancellationToken: Current.CancellationToken);
        await Handler.AssertCollection(5.Seconds(), [..testEvents]).Validate(Current.CancellationToken);
        await Stop();

        CheckpointStore.GetCheckpoint(Subscription.SubscriptionId).Should().Be(count - 1);
    }
}
