using Eventuous.Producers;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.EventStore;

public class PersistentPublishAndSubscribeManyTests(IntegrationFixture fixture, ITestOutputHelper outputHelper)
    : PersistentSubscriptionFixture<TestEventHandler>(fixture, outputHelper, new TestEventHandler(), false) {
    [Fact]
    public async Task SubscribeAndProduceMany() {
        const int count = 10000;

        var testEvents = Auto.CreateMany<TestEvent>(count).ToList();
        Handler.AssertCollection(10.Seconds(), [..testEvents]);

        await Start();

        await Producer.Produce(Stream, testEvents, new Metadata());

        await Handler.Validate();

        await Stop();
    }
}
