using Eventuous.Producers;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.Subs;
using Hypothesist;

namespace Eventuous.Tests.EventStore;

public class PublishAndSubscribeManyPartitionedTests : SubscriptionFixture<TestEventHandler> {
    public PublishAndSubscribeManyPartitionedTests(ITestOutputHelper output)
        : base(
            output,
            new TestEventHandler(TimeSpan.FromMilliseconds(5)),
            false,
            pipe => pipe.AddFilterFirst(new PartitioningFilter(10, x => (x.Message as TestEvent)!.Data))
        ) { }

    [Fact]
    public async Task SubscribeAndProduceMany() {
        const int count = 1000;

        var testEvents = Enumerable.Range(1, count)
            .Select(i => new TestEvent(Auto.Create<string>(), i))
            .ToList();

        Handler.AssertThat().Exactly(count, x => testEvents.Contains(x));

        await Start();
        await Producer.Produce(Stream, testEvents, new Metadata());

        await Handler.Validate(5.Seconds());
        await Stop();
        
        CheckpointStore.Last.Position.Should().Be(count - 1);
    }
}