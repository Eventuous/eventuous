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

public class TestEventHandler : BaseEventHandler {
    readonly ITestOutputHelper? _output;
    readonly TimeSpan           _delay;
    
    public int Count { get; private set; }

    IHypothesis<object>? _hypothesis;

    public TestEventHandler(TimeSpan? delay = null, ITestOutputHelper? output = null) {
        _output = output;
        _delay  = delay ?? TimeSpan.Zero;
    }

    public IHypothesis<object> AssertThat() {
        _hypothesis = Hypothesis.For<object>();
        return _hypothesis;
    }

    public Task Validate(TimeSpan timeout) => EnsureHypothesis.Validate(timeout);

    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        _output?.WriteLine(context.Message!.ToString());
        await Task.Delay(_delay);
        await EnsureHypothesis.Test(context.Message!, context.CancellationToken);
        Count++;
        return EventHandlingStatus.Success;
    }

    public void Reset() => Count = 0;

    IHypothesis<object> EnsureHypothesis =>
        _hypothesis ?? throw new InvalidOperationException("Test handler not specified");
}