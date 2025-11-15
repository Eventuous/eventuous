using Eventuous.Producers;
using Eventuous.TestHelpers.TUnit;
using Eventuous.Tests.KurrentDB.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using Shouldly;

namespace Eventuous.Tests.KurrentDB.Subscriptions;

public class PublishAndSubscribeOneTests() : LegacySubscriptionFixture(null, false) {
    [Test]
    [Category("Stream catch-up subscription")]
    public async Task SubscribeAndProduce(CancellationToken cancellationToken) {
        var testEvent = TestEvent.Create();

        await Start();
        await Producer.Produce(Stream, testEvent, new(), cancellationToken: cancellationToken);
        await Handler.AssertCollection(5.Seconds(), [testEvent]).Validate(cancellationToken);
        await Stop();

        await Task.Delay(100, cancellationToken);
        CheckpointStore.GetCheckpoint(Subscription.SubscriptionId).ShouldBe(0UL);
    }
}
