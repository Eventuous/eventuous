using Eventuous.Producers;
using Eventuous.TestHelpers.TUnit;
using Eventuous.Tests.KurrentDB.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using Shouldly;

namespace Eventuous.Tests.KurrentDB.Subscriptions;

public class PublishAndSubscribeManyTests() : LegacySubscriptionFixture(1.Milliseconds(), false) {
    [Test]
    [Category("Stream catch-up subscription")]
    public async Task SubscribeAndProduceMany(CancellationToken cancellationToken) {
        const int count = 100;

        var testEvents = TestEvent.CreateMany(count).ToList();

        await Start();
        await Producer.Produce(Stream, testEvents, new(), cancellationToken: cancellationToken);
        await Handler.AssertCollection(10.Seconds(), [..testEvents]).Validate(cancellationToken);
        await Stop();

        CheckpointStore.GetCheckpoint(Subscription.SubscriptionId).ShouldBe(count - 1UL);
    }
}
