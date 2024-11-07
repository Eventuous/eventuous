using System.Collections.Immutable;
using JetBrains.Annotations;
using static Eventuous.AggregateFactoryRegistry;
using LoggingExtensions = Eventuous.TestHelpers.TUnit.Logging.LoggingExtensions;

namespace Eventuous.Tests.EventStore.Store;

[ClassDataSource<StoreFixture>]
public class AggregateStoreTests {
    readonly StoreFixture                 _fixture;
    readonly ILogger<AggregateStoreTests> _log;

    public AggregateStoreTests(StoreFixture fixture) {
        _fixture = fixture;
        _fixture.TypeMapper.AddType<TestAggregateEvent>("testAggregateEvent");
        var loggerFactory = LoggingExtensions.GetLoggerFactory();
        _log = loggerFactory.CreateLogger<AggregateStoreTests>();
    }

    [Test]
    [Category("Store")]
    [Obsolete("Obsolete")]
    public async Task AppendedEventShouldBeTraced(CancellationToken cancellationToken) {
        var id        = new TestId(Guid.NewGuid().ToString("N"));
        var aggregate = Instance.CreateInstance<TestAggregate, TestState>();
        aggregate.DoIt("test");
        await _fixture.AggregateStore.Store<TestAggregate, TestState, TestId>(aggregate, id, cancellationToken);
    }

    [Test]
    [Category("Store")]
    [Obsolete("Obsolete")]
    public async Task ShouldReadLongAggregateStream(CancellationToken cancellationToken) {
        const int count = 9000;

        var id        = new TestId(Guid.NewGuid().ToString("N"));
        var initial   = Enumerable.Range(1, count).Select(x => new TestAggregateEvent(x.ToString())).ToArray();
        var aggregate = Instance.CreateInstance<TestAggregate, TestState>();
        var counter   = 0;

        foreach (var data in initial) {
            aggregate.DoIt(data.Data);
            counter++;

            if (counter != 1000) continue;

            _log.LogInformation("Storing batch of events..");
            await _fixture.AggregateStore.Store<TestAggregate, TestState, TestId>(aggregate, id, cancellationToken);
            aggregate = await _fixture.AggregateStore.Load<TestAggregate, TestState, TestId>(id, cancellationToken);
            counter   = 0;
        }

        await _fixture.AggregateStore.Store<TestAggregate, TestState, TestId>(aggregate, id, cancellationToken);

        _log.LogInformation("Loading large aggregate stream..");
        var restored = await _fixture.AggregateStore.Load<TestAggregate, TestState, TestId>(id, cancellationToken);

        restored.State.Values.Count.Should().Be(count);
        restored.State.Values.Should().BeEquivalentTo(aggregate.State.Values);
    }

    [Test]
    [Category("Store")]
    [Obsolete("Obsolete")]
    public async Task ShouldReadAggregateStreamManyTimes(CancellationToken cancellationToken) {
        var id        = new TestId(Guid.NewGuid().ToString("N"));
        var aggregate = Instance.CreateInstance<TestAggregate, TestState>();
        aggregate.DoIt("test");
        await _fixture.AggregateStore.Store<TestAggregate, TestState, TestId>(aggregate, id, cancellationToken);

        const int numberOfReads = 100;

        foreach (var unused in Enumerable.Range(0, numberOfReads)) {
            var read = await _fixture.AggregateStore.Load<TestAggregate, TestState, TestId>(id, cancellationToken);
            read.State.Should().BeEquivalentTo(aggregate.State);
        }
    }

    record TestId(string Value) : Id(Value);

    record TestState : State<TestState> {
        public TestState() => On<TestAggregateEvent>((state, evt) => state with { Values = state.Values.Add(evt.Data) });

        public ImmutableList<string> Values { get; init; } = ImmutableList<string>.Empty;
    }

    [UsedImplicitly]
    class TestAggregate : Aggregate<TestState> {
        public void DoIt(string data) => Apply(new TestAggregateEvent(data));
    }

    record TestAggregateEvent(string Data);
}
