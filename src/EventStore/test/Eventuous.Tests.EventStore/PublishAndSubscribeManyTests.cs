using Eventuous.Producers;
using Eventuous.Sut.Subs;
using Hypothesist;

namespace Eventuous.Tests.EventStore;

public class PublishAndSubscribeManyTests : SubscriptionFixture {
    public PublishAndSubscribeManyTests(ITestOutputHelper outputHelper) : base(outputHelper, false) { }

    [Fact]
    public async Task SubscribeAndProduceMany() {
        const int count = 10000;

        var testEvents = Auto.CreateMany<TestEvent>(count).ToList();
        Handler.AssertThat().Exactly(count, x => testEvents.Contains(x));

        await Start();
        
        await Producer.Produce(Stream, testEvents);

        await Handler.Validate(10.Seconds());

        CheckpointStore.Last.Position.Should().Be(count - 1);

        await Stop();
    }
}