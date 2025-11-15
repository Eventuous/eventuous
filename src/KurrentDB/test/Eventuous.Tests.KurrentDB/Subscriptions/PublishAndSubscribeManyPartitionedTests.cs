using Eventuous.Producers;
using Eventuous.TestHelpers.TUnit;
using Eventuous.Tests.KurrentDB.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using Shouldly;

namespace Eventuous.Tests.KurrentDB.Subscriptions;

public class PublishAndSubscribeManyPartitionedTests() : LegacySubscriptionFixture(5.Milliseconds(), false, new StreamName(Guid.NewGuid().ToString("N"))) {
    [Test]
    [Category("Stream catch-up subscription")]
    public async Task SubscribeAndProduceMany(CancellationToken cancellationToken) {
        const int count = 10;

        var testEvents = TestEvent.CreateMany(count);

        await Start();
        await Producer.Produce(Stream, testEvents, new Metadata(), cancellationToken: cancellationToken);
        await Handler.AssertCollection(5.Seconds(), [..testEvents]).Validate(cancellationToken);
        await Stop();

        CheckpointStore.GetCheckpoint(Subscription.SubscriptionId).ShouldBe(count - 1UL);
    }
}
