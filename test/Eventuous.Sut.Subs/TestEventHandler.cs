using System;
using System.Threading;
using System.Threading.Tasks;
using Eventuous.Subscriptions;
using Hypothesist;

namespace Eventuous.Sut.Subs {
    [EventType("test-event")]
    // ReSharper disable once ClassNeverInstantiated.Global
    public record TestEvent(string Data, int Number);

    public class TestEventHandler : IEventHandler {
        IHypothesis<object>? _hypothesis;

        public TestEventHandler(string queue) => SubscriptionId = queue;

        public string SubscriptionId { get; }

        public IHypothesis<object> AssertThat() {
            _hypothesis = Hypothesis.For<object>();
            return _hypothesis;
        }

        public Task Validate(TimeSpan timeout) => EnsureHypothesis.Validate(timeout);

        public Task HandleEvent(object evt, long? position, CancellationToken cancellationToken)
            => EnsureHypothesis.Test(evt, cancellationToken);

        IHypothesis<object> EnsureHypothesis =>
            _hypothesis ?? throw new InvalidOperationException("Test handler not specified");
    }
}