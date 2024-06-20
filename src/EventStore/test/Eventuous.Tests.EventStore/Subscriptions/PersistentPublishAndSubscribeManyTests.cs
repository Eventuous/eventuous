using Eventuous.Producers;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.EventStore.Subscriptions;

[Collection("Database")]
public class PersistentPublishAndSubscribeManyTests(ITestOutputHelper outputHelper)
    : PersistentSubscriptionFixture<TestEventHandler>(outputHelper, new(), false) {
    [Fact]
    [Trait("Category", "Persistent subscription")]
    public async Task SubscribeAndProduceMany() {
        const int count = 1000;

        var testEvents = Auto.CreateMany<TestEvent>(count).ToList();

        await Start();
        await Producer.Produce(Stream, testEvents, new Metadata());
        await Handler.AssertCollection(10.Seconds(), [..testEvents]).Validate();
        await Stop();
    }
}
