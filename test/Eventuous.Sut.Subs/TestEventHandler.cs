using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Hypothesist;

namespace Eventuous.Sut.Subs;

[EventType("test-event")]
// ReSharper disable once ClassNeverInstantiated.Global
public record TestEvent(string Data, int Number);

public class TestEventHandler : BaseEventHandler {
    IHypothesis<object>? _hypothesis;

    public IHypothesis<object> AssertThat() {
        _hypothesis = Hypothesis.For<object>();
        return _hypothesis;
    }

    public Task Validate(TimeSpan timeout) => EnsureHypothesis.Validate(timeout);

    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        await EnsureHypothesis.Test(context.Message!, context.CancellationToken);
        return EventHandlingStatus.Success;
    }

    IHypothesis<object> EnsureHypothesis =>
        _hypothesis ?? throw new InvalidOperationException("Test handler not specified");
}