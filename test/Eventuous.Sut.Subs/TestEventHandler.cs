using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Logging;
using Hypothesist;

namespace Eventuous.Sut.Subs;

[EventType("test-event")]
// ReSharper disable once ClassNeverInstantiated.Global
public record TestEvent(string Data, int Number);

public class TestEventHandler : IEventHandler {
    IHypothesis<object>? _hypothesis;

    public IHypothesis<object> AssertThat() {
        _hypothesis = Hypothesis.For<object>();
        return _hypothesis;
    }

    public Task Validate(TimeSpan timeout) => EnsureHypothesis.Validate(timeout);

    public void SetLogger(SubscriptionLog subscriptionLogger) { }

    public Task HandleEvent(ReceivedEvent evt, CancellationToken cancellationToken)
        => EnsureHypothesis.Test(evt.Payload!, cancellationToken);

    IHypothesis<object> EnsureHypothesis =>
        _hypothesis ?? throw new InvalidOperationException("Test handler not specified");
}