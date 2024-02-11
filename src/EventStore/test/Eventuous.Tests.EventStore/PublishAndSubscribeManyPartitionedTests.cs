using Eventuous.Producers;
using Eventuous.Sut.Subs;

namespace Eventuous.Tests.EventStore;

public class PublishAndSubscribeManyPartitionedTests(IntegrationFixture fixture, ITestOutputHelper output)
    : SubscriptionFixture<TestEventHandler>(fixture, output, new TestEventHandler(5.Milliseconds()), false, logLevel: LogLevel.Trace) {
    [Fact]
    public async Task SubscribeAndProduceMany() {
        const int count = 10;

        var testEvents = Enumerable.Range(1, count)
            .Select(i => new TestEvent(Auto.Create<string>(), i))
            .ToList();

        Handler.AssertCollection(5.Seconds(), [..testEvents]);

        await Start();
        await Producer.Produce(Stream, testEvents, new Metadata());

        await Handler.Validate();
        await Stop();

        CheckpointStore.Last.Position.Should().Be(count - 1);
    }
}
