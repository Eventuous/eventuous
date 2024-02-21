using Eventuous.Producers;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.EventStore.Subscriptions;

[Collection("Database")]
public class PublishAndSubscribeOneTests(ITestOutputHelper outputHelper)
    : LegacySubscriptionFixture<TestEventHandler>(outputHelper, new TestEventHandler(output: outputHelper), false, logLevel: LogLevel.Trace) {
    [Fact]
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
