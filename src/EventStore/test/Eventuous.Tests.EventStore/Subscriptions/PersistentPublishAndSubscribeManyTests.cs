using Eventuous.Producers;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.EventStore.Subscriptions;

[Collection("Database")]
public class PersistentPublishAndSubscribeManyTests(StoreFixture fixture, ITestOutputHelper outputHelper)
    : PersistentSubscriptionFixture<TestEventHandler>(fixture, outputHelper, new TestEventHandler(), false) {
    [Fact]
    public async Task SubscribeAndProduceMany() {
        const int count = 1000;

        var testEvents = Auto.CreateMany<TestEvent>(count).ToList();

        await Start();
        await Producer.Produce(Stream, testEvents, new Metadata());
        await Handler.AssertCollection(10.Seconds(), [..testEvents]).Validate();
        await Stop();
    }
}
