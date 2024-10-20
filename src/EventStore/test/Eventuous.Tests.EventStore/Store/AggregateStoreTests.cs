using System.Collections.Immutable;
using Eventuous.TestHelpers.Logging;
using JetBrains.Annotations;
using static Eventuous.AggregateFactoryRegistry;
using static Xunit.TestContext;

namespace Eventuous.Tests.EventStore.Store;

public class AggregateStoreTests : IClassFixture<StoreFixture> {
    readonly StoreFixture                 _fixture;
    readonly ILogger<AggregateStoreTests> _log;

    public AggregateStoreTests(StoreFixture fixture, ITestOutputHelper output) {
        _fixture = fixture;
        _fixture.TypeMapper.AddType<TestAggregateEvent>("testAggregateEvent");
        var loggerFactory = LoggerFactory.Create(cfg => cfg.AddXUnit(output).SetMinimumLevel(LogLevel.Debug));
        _log = loggerFactory.CreateLogger<AggregateStoreTests>();
    }

    [Fact]
    [Trait("Category", "Store")]
    [Obsolete("Obsolete")]
    public async Task AppendedEventShouldBeTraced() {
        var id        = new TestId(Guid.NewGuid().ToString("N"));
        var aggregate = Instance.CreateInstance<TestAggregate, TestState>();
        aggregate.DoIt("test");
        await _fixture.AggregateStore.Store<TestAggregate, TestState, TestId>(aggregate, id, CancellationToken.None);
    }

    [Fact]
    [Trait("Category", "Store")]
    [Obsolete("Obsolete")]
    public async Task ShouldReadLongAggregateStream() {
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
            await _fixture.AggregateStore.Store<TestAggregate, TestState, TestId>(aggregate, id, CancellationToken.None);
            aggregate = await _fixture.AggregateStore.Load<TestAggregate, TestState, TestId>(id, CancellationToken.None);
            counter   = 0;
        }

        await _fixture.AggregateStore.Store<TestAggregate, TestState, TestId>(aggregate, id, CancellationToken.None);

        _log.LogInformation("Loading large aggregate stream..");
        var restored = await _fixture.AggregateStore.Load<TestAggregate, TestState, TestId>(id, CancellationToken.None);

        restored.State.Values.Count.Should().Be(count);
        restored.State.Values.Should().BeEquivalentTo(aggregate.State.Values);
    }

    [Fact]
    [Trait("Category", "Store")]
    [Obsolete("Obsolete")]
    public async Task ShouldReadAggregateStreamManyTimes() {
        var id        = new TestId(Guid.NewGuid().ToString("N"));
        var aggregate = Instance.CreateInstance<TestAggregate, TestState>();
        aggregate.DoIt("test");
        await _fixture.AggregateStore.Store<TestAggregate, TestState, TestId>(aggregate, id, Current.CancellationToken);

        const int numberOfReads = 100;

        foreach (var unused in Enumerable.Range(0, numberOfReads)) {
            var read = await _fixture.AggregateStore.Load<TestAggregate, TestState, TestId>(id, Current.CancellationToken);
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
