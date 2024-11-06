using Eventuous.Producers;
using Eventuous.Tests.EventStore.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.EventStore.Subscriptions;

public class PublishAndSubscribeOneTests() : LegacySubscriptionFixture(null, false) {
    [Test]
    [Category("Stream catch-up subscription")]
    public async Task SubscribeAndProduce(CancellationToken cancellationToken) {
        var testEvent = Auto.Create<TestEvent>();

        await Start();
        await Producer.Produce(Stream, testEvent, new(), cancellationToken: cancellationToken);
        await Handler.AssertCollection(5.Seconds(), [testEvent]).Validate(cancellationToken);
        await Stop();

        await Task.Delay(100, cancellationToken);
        CheckpointStore.GetCheckpoint(Subscription.SubscriptionId).Should().Be(0);
    }
}
