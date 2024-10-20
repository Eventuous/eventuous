using Eventuous.Producers;
using Eventuous.Tests.EventStore.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using static Xunit.TestContext;

namespace Eventuous.Tests.EventStore.Subscriptions;

[Collection("Database")]
public class PublishAndSubscribeManyTests(ITestOutputHelper output)
    : LegacySubscriptionFixture<TestEventHandler>(output, new(new(1.Milliseconds(), output)), false, logLevel: LogLevel.Debug) {
    [Fact]
    [Trait("Category", "Stream catch-up subscription")]
    public async Task SubscribeAndProduceMany() {
        const int count = 100;

        var testEvents = Auto.CreateMany<TestEvent>(count).ToList();

        await Start();
        await Producer.Produce(Stream, testEvents, new(), cancellationToken: Current.CancellationToken);
        await Handler.AssertCollection(10.Seconds(), [..testEvents]).Validate(Current.CancellationToken);
        await Stop();

        CheckpointStore.GetCheckpoint(Subscription.SubscriptionId).Should().Be(count - 1);
    }
}
