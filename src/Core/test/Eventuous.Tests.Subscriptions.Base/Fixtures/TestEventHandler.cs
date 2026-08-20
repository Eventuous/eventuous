using System.Collections.Concurrent;
using Bogus;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Hypothesist;
using Hypothesist.Builders;

// ReSharper disable NotAccessedPositionalProperty.Global
// ReSharper disable MethodHasAsyncOverload

namespace Eventuous.Tests.Subscriptions.Base;

[EventType(TypeName)]
// ReSharper disable once ClassNeverInstantiated.Global
public record TestEvent(string Data, int Number) {
    public const string TypeName = "test-event";

    static readonly Faker<TestEvent> Faker = new Faker<TestEvent>().CustomInstantiator(f => new(f.Lorem.Sentence(), f.Random.Int()));

    public static TestEvent Create() => Faker.Generate();

    public static List<TestEvent> CreateMany(int count) => Faker.Generate(count);
}

public class TestEventHandler(TestEventHandlerOptions? options) : BaseEventHandler {
    public TestEventHandler() : this(null) { }

    readonly TimeSpan _delay = options?.Delay ?? TimeSpan.Zero;

    public int Count { get; private set; }

    readonly Observer<object>       _observer = new();
    readonly ConcurrentQueue<object> _handled = new();

    public On<object> AssertThat() => Hypothesis.On(_observer);

    /// <summary>
    /// Expects exactly <paramref name="collection"/> to be handled within <paramref name="deadline"/>. Takes
    /// the whole deadline: proving "n and no more" means watching the window out, so pick one that suits the
    /// transport rather than one padded for the worst case.
    /// </summary>
    /// <remarks>
    /// An empty expectation matches everything instead, since <c>Contains</c> on an empty collection rejects
    /// every message and would leave nothing for <c>Exactly(0)</c> to count — an assertion that cannot fail.
    /// </remarks>
    public Hypothesis<object> AssertCollection(TimeSpan deadline, List<object> collection)
        => collection.Count == 0
            ? Hypothesis.On(_observer).Timebox(deadline).AtMost(0).Match(_ => true)
            : Hypothesis.On(_observer).Timebox(deadline).Exactly(collection.Count).Match(collection.Contains);

    /// <summary>
    /// Messages handled so far. Backed by a concurrent queue so tests can poll it while the subscription
    /// keeps handling on background threads.
    /// </summary>
    public IReadOnlyCollection<object> Handled => [.. _handled];

    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        await Task.Delay(_delay);
        await _observer.Add(context.Message!, context.CancellationToken);
        _handled.Enqueue(context.Message!);
        Count++;

        return EventHandlingStatus.Success;
    }

    public void Reset() {
        Count = 0;
        _handled.Clear();
    }
}

public record TestEventHandlerOptions(TimeSpan? Delay = null);

