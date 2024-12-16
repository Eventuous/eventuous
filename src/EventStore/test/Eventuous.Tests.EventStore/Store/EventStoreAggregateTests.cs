using System.Collections.Immutable;
using JetBrains.Annotations;
using static Eventuous.AggregateFactoryRegistry;
using LoggingExtensions = Eventuous.TestHelpers.TUnit.Logging.LoggingExtensions;

namespace Eventuous.Tests.EventStore.Store;

[ClassDataSource<StoreFixture>]
public class EventStoreAggregateTests {
    readonly StoreFixture                 _fixture;
    readonly ILogger<AggregateStoreTests> _log;
    readonly ILoggerFactory               _loggerFactory;

    public EventStoreAggregateTests(StoreFixture fixture) {
        _fixture = fixture;
        _fixture.TypeMapper.AddType<TestEvent>("testEvent");
        _loggerFactory = LoggingExtensions.GetLoggerFactory();
        _log           = _loggerFactory.CreateLogger<AggregateStoreTests>();
    }

    [Test]
    [Category("Store")]
    public async Task AppendedEventShouldBeTraced(CancellationToken cancellationToken) {
        var id        = new TestId(Guid.NewGuid().ToString("N"));
        var aggregate = Instance.CreateInstance<TestAggregate, TestState>();
        aggregate.DoIt("test");
        await _fixture.EventStore.StoreAggregate<TestAggregate, TestState, TestId>(aggregate, id, cancellationToken: cancellationToken);

        var streamName = StreamNameFactory.For<TestAggregate, TestState, TestId>(id);
        var events     = await _fixture.EventStore.ReadStream(streamName, StreamReadPosition.Start, cancellationToken: cancellationToken);
        var first      = events[0];

        first.Metadata["$traceId"].Should().NotBeNull();
        first.Metadata["$spanId"].Should().NotBeNull();
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReadLongAggregateStream(CancellationToken cancellationToken) {
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
            await _fixture.EventStore.StoreAggregate<TestAggregate, TestState, TestId>(aggregate, id, cancellationToken: cancellationToken);
            aggregate = await _fixture.EventStore.LoadAggregate<TestAggregate, TestState, TestId>(id, cancellationToken: cancellationToken);
            counter   = 0;
        }

        await _fixture.EventStore.StoreAggregate<TestAggregate, TestState, TestId>(aggregate, id, cancellationToken: cancellationToken);

        _log.LogInformation("Loading large aggregate stream..");
        var restored = await _fixture.EventStore.LoadAggregate<TestAggregate, TestState, TestId>(id, cancellationToken: cancellationToken);

        restored.State.Values.Count.Should().Be(count);
        restored.State.Values.Should().BeEquivalentTo(aggregate.State.Values);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReadAggregateStreamManyTimes(CancellationToken cancellationToken) {
        var id        = new TestId(Guid.NewGuid().ToString("N"));
        var aggregate = Instance.CreateInstance<TestAggregate, TestState>();
        aggregate.DoIt("test");
        await _fixture.EventStore.StoreAggregate<TestAggregate, TestState, TestId>(aggregate, id, cancellationToken: cancellationToken);

        const int numberOfReads = 100;

        foreach (var unused in Enumerable.Range(0, numberOfReads)) {
            var read = await _fixture.EventStore.LoadAggregate<TestAggregate, TestState, TestId>(id, cancellationToken: cancellationToken);
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

    [After(Test)]
    public void Dispose() => _loggerFactory.Dispose();
}
