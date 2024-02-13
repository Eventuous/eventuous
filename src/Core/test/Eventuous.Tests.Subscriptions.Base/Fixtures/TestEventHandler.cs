using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Hypothesist;
using Hypothesist.Builders;

namespace Eventuous.Tests.Subscriptions.Base;

[EventType(TypeName)]
// ReSharper disable once ClassNeverInstantiated.Global
public record TestEvent(string Data, int Number) {
    public const string TypeName = "test-event";
}

public class TestEventHandler(TimeSpan? delay = null, ITestOutputHelper? output = null) : BaseEventHandler {
    readonly TimeSpan _delay = delay ?? TimeSpan.Zero;

    public int Count { get; private set; }

    readonly Observer<object> _observer = new();
    Hypothesis<object>?       _hypothesis;

    public void AssertThat(TimeSpan deadline, Func<Timebox<object>, Hypothesis<object>> getHypothesis) {
        var builder = Hypothesis.On(_observer);

        _hypothesis = getHypothesis(builder.Timebox(deadline));
    }

    public void AssertCollection(TimeSpan deadline, List<object> collection) {
        var builder = Hypothesis.On(_observer);

        _hypothesis = builder.Timebox(deadline).Exactly(collection.Count).Match(collection.Contains);
    }

    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        output?.WriteLine(context.Message!.ToString());
        await Task.Delay(_delay);
        await _observer.Add(context.Message!, context.CancellationToken);
        Count++;

        return EventHandlingStatus.Success;
    }

    public Task Validate() => EnsureHypothesis.Validate();

    public void Reset() => Count = 0;

    Hypothesis<object> EnsureHypothesis =>
        _hypothesis ?? throw new InvalidOperationException("Test handler not specified");
}
