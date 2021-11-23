using Eventuous.Producers;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.Subs;
using Hypothesist;

namespace Eventuous.Tests.EventStore;

public class PublishAndSubscribeManyPartitionedTests : SubscriptionFixture<TestEventHandler> {
    public PublishAndSubscribeManyPartitionedTests(ITestOutputHelper outputHelper)
        : base(
            outputHelper,
            new TestEventHandler(),
            false,
            pipe => pipe.AddFilterFirst(new PartitioningFilter(10)),
            new StreamName("$ce-part")
        ) { }

    [Fact]
    public async Task SubscribeAndProduceMany() {
        const int count = 100;

        var testEvents = Auto.CreateMany<TestEvent>(count).ToList();
        Handler.AssertThat().Exactly(count, x => testEvents.Contains(x));

        await Start();

        foreach (var testEvent in testEvents) {
            await Producer.Produce(new StreamName($"part-{testEvent.Number}"), testEvent);
        }

        await Handler.Validate(5.Seconds());

        CheckpointStore.Last.Position.Should().Be(count - 1);

        await Stop();
    }
}