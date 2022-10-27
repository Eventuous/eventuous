using System.Collections.Immutable;

namespace Eventuous.Tests.EventStore;

public class AggregateStoreTests {
    readonly ILogger<AggregateStoreTests> _log;

    public AggregateStoreTests(ITestOutputHelper output) {
        Store = IntegrationFixture.Instance.AggregateStore;
        TypeMap.Instance.AddType<TestEvent>("testEvent");

        var loggerFactory = LoggerFactory.Create(
            cfg => cfg.AddXunit(output).SetMinimumLevel(LogLevel.Debug)
        );

        _log = loggerFactory.CreateLogger<AggregateStoreTests>();
    }

    [Fact]
    public async Task AppendedEventShouldBeTraced() {
        var id        = new TestId(Guid.NewGuid().ToString("N"));
        var aggregate = AggregateFactoryRegistry.Instance.CreateInstance<TestAggregate>();
        aggregate.DoIt("test");
        await Store.Store(aggregate, id, CancellationToken.None);
    }

    [Fact]
    public async Task ShouldReadLongAggregateStream() {
        const int count = 9000;

        var id = new TestId(Guid.NewGuid().ToString("N"));

        var initial = Enumerable
            .Range(1, count)
            .Select(x => new TestEvent(x.ToString()))
            .ToArray();

        var aggregate = AggregateFactoryRegistry.Instance.CreateInstance<TestAggregate>();

        var counter = 0;

        foreach (var data in initial) {
            aggregate.DoIt(data.Data);
            counter++;

            if (counter != 1000) continue;

            _log.LogInformation("Storing batch of events..");
            await Store.Store(aggregate, id, CancellationToken.None);
            aggregate = await Store.Load<TestAggregate, TestId>(id, CancellationToken.None);
            counter   = 0;
        }

        await Store.Store(aggregate, id, CancellationToken.None);

        _log.LogInformation("Loading large aggregate stream..");
        var restored = await Store.Load<TestAggregate, TestId>(id, CancellationToken.None);

        restored.State.Values.Count.Should().Be(count);
        restored.State.Values.Should().BeEquivalentTo(aggregate.State.Values);
    }

    [Fact]
    public async Task ShouldReadAggregateStreamManyTimes() {
        var id        = new TestId(Guid.NewGuid().ToString("N"));
        var aggregate = AggregateFactoryRegistry.Instance.CreateInstance<TestAggregate>();
        aggregate.DoIt("test");
        await Store.Store(aggregate,id, default);

        const int numberOfReads = 100;

        foreach (var unused in Enumerable.Range(0, numberOfReads)) {
            var read = await Store.Load<TestAggregate, TestId>(id, default);
            read.State.Should().BeEquivalentTo(aggregate.State);
        }
    }

    IAggregateStore Store { get; }

    record TestId : AggregateId {
        public TestId(string value) : base(value) { }
    }

    record TestState : State<TestState> {
        public TestState()
            => On<TestEvent>(
                (state, evt) => state with { Values = state.Values.Add(evt.Data) }
            );

        public ImmutableList<string> Values { get; init; } = ImmutableList<string>.Empty;
    }

    class TestAggregate : Aggregate<TestState> {
        public void DoIt(string data) => Apply(new TestEvent(data));
    }

    record TestEvent(string Data);
}
