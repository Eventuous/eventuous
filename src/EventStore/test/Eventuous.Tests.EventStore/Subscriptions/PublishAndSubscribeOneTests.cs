using Eventuous.Producers;
using Eventuous.Tests.EventStore.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using static Xunit.TestContext;

namespace Eventuous.Tests.EventStore.Subscriptions;

[Collection("Database")]
public class PublishAndSubscribeOneTests(ITestOutputHelper output)
    : LegacySubscriptionFixture<TestEventHandler>(output, new(new(null, output)), false, logLevel: LogLevel.Debug) {
    [Fact]
    [Trait("Category", "Stream catch-up subscription")]
    public async Task SubscribeAndProduce() {
        var testEvent = Auto.Create<TestEvent>();

        await Start();
        await Producer.Produce(Stream, testEvent, new Metadata(), cancellationToken: Current.CancellationToken);
        await Handler.AssertCollection(5.Seconds(), [testEvent]).Validate(Current.CancellationToken);
        await Stop();

        await Task.Delay(100, Current.CancellationToken);
        CheckpointStore.GetCheckpoint(Subscription.SubscriptionId).Should().Be(0);
    }
}
