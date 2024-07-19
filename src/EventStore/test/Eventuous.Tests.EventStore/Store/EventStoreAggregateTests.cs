using System.Collections.Immutable;
using JetBrains.Annotations;
using static Eventuous.AggregateFactoryRegistry;

namespace Eventuous.Tests.EventStore.Store;

public class EventStoreAggregateTests : IClassFixture<StoreFixture>, IDisposable {
    readonly StoreFixture                 _fixture;
    readonly ILogger<AggregateStoreTests> _log;
    readonly ILoggerFactory               _loggerFactory;

    public EventStoreAggregateTests(StoreFixture fixture, ITestOutputHelper output) {
        _fixture = fixture;
        _fixture.TypeMapper.AddType<TestEvent>("testEvent");
        _loggerFactory = LoggerFactory.Create(cfg => cfg.AddXunit(output).SetMinimumLevel(LogLevel.Debug));
        _log           = _loggerFactory.CreateLogger<AggregateStoreTests>();
    }

    [Fact]
    [Trait("Category", "Store")]
    public async Task AppendedEventShouldBeTraced() {
        var id        = new TestId(Guid.NewGuid().ToString("N"));
        var aggregate = Instance.CreateInstance<TestAggregate, TestState>();
        aggregate.DoIt("test");
        await _fixture.EventStore.StoreAggregate<TestAggregate, TestState, TestId>(aggregate, id);

        var streamName = StreamNameFactory.For<TestAggregate, TestState, TestId>(id);
        var events     = await _fixture.EventStore.ReadStream(streamName, StreamReadPosition.Start);
        var first      = events[0];

        first.Metadata["trace-id"].Should().NotBeNull();
        first.Metadata["span-id"].Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Store")]
    public async Task ShouldReadLongAggregateStream() {
        const int count = 9000;

        var id        = new TestId(Guid.NewGuid().ToString("N"));
        var initial   = Enumerable.Range(1, count).Select(x => new TestEvent(x.ToString())).ToArray();
        var aggregate = Instance.CreateInstance<TestAggregate, TestState>();
        var counter   = 0;

        foreach (var data in initial) {
            aggregate.DoIt(data.Data);
            counter++;

            if (counter != 1000) continue;

            _log.LogInformation("Storing batch of events..");
            await _fixture.EventStore.StoreAggregate<TestAggregate, TestState, TestId>(aggregate, id);
            aggregate = await _fixture.EventStore.LoadAggregate<TestAggregate, TestState, TestId>(id);
            counter   = 0;
        }

        await _fixture.EventStore.StoreAggregate<TestAggregate, TestState, TestId>(aggregate, id);

        _log.LogInformation("Loading large aggregate stream..");
        var restored = await _fixture.EventStore.LoadAggregate<TestAggregate, TestState, TestId>(id);

        restored.State.Values.Count.Should().Be(count);
        restored.State.Values.Should().BeEquivalentTo(aggregate.State.Values);
    }

    [Fact]
    [Trait("Category", "Store")]
    public async Task ShouldReadAggregateStreamManyTimes() {
        var id        = new TestId(Guid.NewGuid().ToString("N"));
        var aggregate = Instance.CreateInstance<TestAggregate, TestState>();
        aggregate.DoIt("test");
        await _fixture.EventStore.StoreAggregate<TestAggregate, TestState, TestId>(aggregate, id);

        const int numberOfReads = 100;

        foreach (var unused in Enumerable.Range(0, numberOfReads)) {
            var read = await _fixture.EventStore.LoadAggregate<TestAggregate, TestState, TestId>(id);
            read.State.Should().BeEquivalentTo(aggregate.State);
        }
    }

    record TestId(string Value) : Id(Value);

    record TestState : State<TestState> {
        public TestState() => On<TestEvent>((state, evt) => state with { Values = state.Values.Add(evt.Data) });

        public ImmutableList<string> Values { get; init; } = ImmutableList<string>.Empty;
    }

    [UsedImplicitly]
    class TestAggregate : Aggregate<TestState> {
        public void DoIt(string data) => Apply(new TestEvent(data));
    }

    record TestEvent(string Data);

    public void Dispose() => _loggerFactory.Dispose();
}
