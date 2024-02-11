using Eventuous.Producers;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.EventStore;

public class PublishAndSubscribeManyTests(IntegrationFixture fixture, ITestOutputHelper outputHelper)
    : SubscriptionFixture<TestEventHandler>(fixture, outputHelper, new TestEventHandler(TimeSpan.FromMilliseconds(1)), false, logLevel: LogLevel.Trace) {
    [Fact]
    public async Task SubscribeAndProduceMany() {
        const int count = 100;

        var testEvents = Auto.CreateMany<TestEvent>(count).ToList();
        Handler.AssertCollection(10.Seconds(), [..testEvents]);

        await Start();
        await Producer.Produce(Stream, testEvents, new Metadata());
        await Handler.Validate();
        await Stop();

        CheckpointStore.Last.Position.Should().Be(count - 1);
    }
}
