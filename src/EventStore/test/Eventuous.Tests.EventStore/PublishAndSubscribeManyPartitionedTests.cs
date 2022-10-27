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
            logLevel: LogLevel.Trace
        ) { }

    [Fact]
    public async Task SubscribeAndProduceMany() {
        const int count = 10;

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

    [Fact]
    public async Task SubscribeAndProduceManyWithIgnored() {
        const int count = 10;

        var testEvents = Generate().ToList();

        Handler.AssertThat().Exactly(count, x => testEvents.Contains(x));

        TypeMap.Instance.AddType<UnknownEvent>("ignored");
        await Producer.Produce(Stream, testEvents, new Metadata());

        await Start();
        TypeMap.Instance.RemoveType<UnknownEvent>();
        await Handler.Validate(5.Seconds());
        await Stop();

        CheckpointStore.Last.Position.Should().Be((ulong)(testEvents.Count - 1));

        IEnumerable<object> Generate() {
            for (var i = 0; i < count; i++) {
                yield return new TestEvent(Auto.Create<string>(), i);
                yield return new UnknownEvent(Auto.Create<string>(), i);
            }
        }
    }

    record UnknownEvent(string Data, int Number);
}
