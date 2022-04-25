using Eventuous.Producers;
using Eventuous.Sut.Subs;
using Hypothesist;

namespace Eventuous.Tests.EventStore;

public class PersistentPublishAndSubscribeManyTests : PersistentSubscriptionFixture<TestEventHandler> {
    public PersistentPublishAndSubscribeManyTests(ITestOutputHelper outputHelper) 
        : base(outputHelper, new TestEventHandler(), false) { }

    [Fact]
    public async Task SubscribeAndProduceMany() {
        const int count = 10000;

        var testEvents = Auto.CreateMany<TestEvent>(count).ToList();
        Handler.AssertThat().Exactly(count, x => testEvents.Contains(x));

        await Start();
        
        await Producer.Produce(Stream, testEvents, new Metadata());

        await Handler.Validate(10.Seconds());

        await Stop();
    }
}