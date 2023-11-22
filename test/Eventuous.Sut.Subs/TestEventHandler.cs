using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Hypothesist;
using Xunit.Abstractions;

namespace Eventuous.Sut.Subs;

[EventType(TypeName)]
// ReSharper disable once ClassNeverInstantiated.Global
public record TestEvent(string Data, int Number) {
    public const string TypeName = "test-event";
}

public class TestEventHandler(TimeSpan? delay = null, ITestOutputHelper? output = null) : BaseEventHandler {
    readonly TimeSpan _delay = delay ?? TimeSpan.Zero;
    readonly string   _id    = Guid.NewGuid().ToString("N");

    public int Count { get; private set; }

    IHypothesis<object>? _hypothesis;

    public IHypothesis<object> AssertThat() {
        _hypothesis = Hypothesis.For<object>();

        return _hypothesis;
    }

    public Task Validate(TimeSpan timeout) => EnsureHypothesis.Validate(timeout);

    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        await Task.Delay(_delay);
        await EnsureHypothesis.Test(context.Message!, context.CancellationToken);
        output?.WriteLine($"[{_id}] Handled event {context.GlobalPosition}, count is {Count}");
        Count++;

        return EventHandlingStatus.Success;
    }

    public void Reset() => Count = 0;

    IHypothesis<object> EnsureHypothesis =>
        _hypothesis ?? throw new InvalidOperationException("Test handler not specified");
}
