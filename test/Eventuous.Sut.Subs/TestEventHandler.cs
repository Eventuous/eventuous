using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Hypothesist;
using Hypothesist.Builders;
using Xunit.Abstractions;

namespace Eventuous.Sut.Subs;

[EventType(TypeName)]
// ReSharper disable once ClassNeverInstantiated.Global
public record TestEvent(string Data, int Number) {
    public const string TypeName = "test-event";
}

public class TestEventHandler(TimeSpan? delay = null, ITestOutputHelper? output = null) : BaseEventHandler {
    readonly TimeSpan _delay = delay ?? TimeSpan.Zero;

    public int Count { get; private set; }

    readonly Observer<object> _observer = new();

    public On<object> AssertThat() => Hypothesis.On(_observer);

    public Hypothesis<object> AssertCollection(TimeSpan deadline, List<object> collection) => Hypothesis.On(_observer).Timebox(deadline).Exactly(collection.Count).Match(collection.Contains);

    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        output?.WriteLine(context.Message!.ToString());
        await Task.Delay(_delay);
        await _observer.Add(context.Message!, context.CancellationToken);
        Count++;

        return EventHandlingStatus.Success;
    }

    public void Reset() => Count = 0;
}
