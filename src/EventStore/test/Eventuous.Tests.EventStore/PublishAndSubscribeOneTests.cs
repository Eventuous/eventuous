using Eventuous.Producers;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.EventStore;

public class PublishAndSubscribeOneTests(IntegrationFixture fixture, ITestOutputHelper outputHelper)
    : SubscriptionFixture<TestEventHandler>(fixture, outputHelper, new TestEventHandler(), false, logLevel: LogLevel.Trace) {
    [Fact]
    public async Task SubscribeAndProduce() {
        var testEvent = Auto.Create<TestEvent>();
        Handler.AssertCollection(5.Seconds(), [testEvent]);

        await Start();
        await Producer.Produce(Stream, testEvent, new Metadata());
        await Handler.Validate();
        await Stop();

        await Task.Delay(100);
        CheckpointStore.Last.Position.Should().Be(0);
    }
}
