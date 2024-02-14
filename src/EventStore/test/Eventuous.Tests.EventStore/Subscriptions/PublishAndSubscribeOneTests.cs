using Eventuous.Producers;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.EventStore.Subscriptions;

public class PublishAndSubscribeOneTests(StoreFixture fixture, ITestOutputHelper outputHelper)
    : LegacySubscriptionFixture<TestEventHandler>(fixture, outputHelper, new TestEventHandler(), false, logLevel: LogLevel.Trace) {
    [Fact]
    public async Task SubscribeAndProduce() {
        var testEvent = Auto.Create<TestEvent>();
        Handler.AssertCollection(5.Seconds(), [testEvent]);

        await Start();
        await Producer.Produce(Stream, testEvent, new Metadata());
        await Handler.Validate();
        await Stop();

        await Task.Delay(100);
        CheckpointStore.GetCheckpoint(Subscription.SubscriptionId).Should().Be(0);
    }
}
