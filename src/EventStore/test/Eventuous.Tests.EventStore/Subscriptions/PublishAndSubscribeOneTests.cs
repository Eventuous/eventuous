using Eventuous.Producers;
using Eventuous.Tests.EventStore.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.EventStore.Subscriptions;

[Collection("Database")]
public class PublishAndSubscribeOneTests(ITestOutputHelper output)
    : LegacySubscriptionFixture<TestEventHandler>(output, new(new(null, output)), false, logLevel: LogLevel.Trace) {
    [Fact]
    [Trait("Category", "Stream catch-up subscription")]
    public async Task SubscribeAndProduce() {
        var testEvent = Auto.Create<TestEvent>();

        await Start();
        await Producer.Produce(Stream, testEvent, new Metadata());
        await Handler.AssertCollection(5.Seconds(), [testEvent]).Validate();
        await Stop();

        await Task.Delay(100);
        CheckpointStore.GetCheckpoint(Subscription.SubscriptionId).Should().Be(0);
    }
}
