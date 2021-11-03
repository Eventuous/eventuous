using Eventuous.Producers;
using Eventuous.Sut.Subs;
using Hypothesist;

namespace Eventuous.Tests.EventStore;

public class PublishAndSubscribeOneTests : SubscriptionFixture<TestEventHandler> {
    public PublishAndSubscribeOneTests(ITestOutputHelper outputHelper) 
        : base(outputHelper, new TestEventHandler()) { }

    [Fact]
    public async Task SubscribeAndProduce() {
        var testEvent = Auto.Create<TestEvent>();
        Handler.AssertThat().Any(x => x as TestEvent == testEvent);

        await Producer.Produce(Stream, testEvent);

        await Handler.Validate(10.Seconds());

        CheckpointStore.Last.Position.Should().Be(0);
    }
}