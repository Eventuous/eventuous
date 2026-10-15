using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Assertions.Enums;

namespace Eventuous.Tests.Subscriptions;

[NotInParallel]
public class TracedEventHandlerTests {
    static MessageConsumeContext CreateContext(string messageType = "TestEvent")
        => new(
            Guid.NewGuid().ToString(),
            messageType,
            "application/json",
            "test-stream",
            0,
            0,
            0,
            0,
            DateTime.UtcNow,
            new object(),
            null,
            "test-subscription",
            CancellationToken.None
        );

    [Test]
    public async Task ShouldMeasureEveryTracedHandler() {
        var handlers = new List<object?>();

        using var meterListener = new MeterListener {
            InstrumentPublished = (instrument, listener) => {
                if (instrument.Name == SubscriptionMetrics.ProcessingRateName) listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<double>(
            (_, _, tags, _) => {
                foreach (var tag in tags) {
                    if (tag.Key != SubscriptionMetrics.EventHandlerTag) continue;

                    lock (handlers) handlers.Add(tag.Value);
                }
            }
        );
        meterListener.Start();

        // The metrics listener has to exist before the handlers: each handler used to announce its own diagnostic
        // listener, and the metrics listener only kept the last one it saw.
        using var metrics = new SubscriptionMetrics([]);

        var first  = new TracedEventHandler(new TracedFirstHandler());
        var second = new TracedEventHandler(new TracedSecondHandler());

        await first.HandleEvent(CreateContext());
        await second.HandleEvent(CreateContext());

        await Assert.That(handlers).Contains(nameof(TracedFirstHandler));
        await Assert.That(handlers).Contains(nameof(TracedSecondHandler));
    }

    [Test]
    public async Task ShouldNameHandlerActivitiesAfterHandlerAndMessageType() {
        var names = new List<string>();

        using var listener = StartActivityListener(names, "handler.");

        var handler = new TracedEventHandler(new TracedFirstHandler());

        // Repeated types exercise the cached names, which must match the names built per event before them.
        await handler.HandleEvent(CreateContext("TestEvent"));
        await handler.HandleEvent(CreateContext("OtherEvent"));
        await handler.HandleEvent(CreateContext("TestEvent"));

        await Assert.That(names).IsEquivalentTo(
            ["handler.TracedFirstHandler/TestEvent", "handler.TracedFirstHandler/OtherEvent", "handler.TracedFirstHandler/TestEvent"],
            CollectionOrdering.Matching
        );
    }

    [Test]
    public async Task ShouldNameSubscriptionActivitiesAfterSubscriptionAndMessageType() {
        var names = new List<string>();

        using var listener = StartActivityListener(names, "sub.");

        await using var subscription = new TestSubscription(new ConsumePipe().AddDefaultConsumer(new TracedFirstHandler()));

        await subscription.Deliver(CreateContext("TestEvent"));
        await subscription.Deliver(CreateContext("OtherEvent"));
        await subscription.Deliver(CreateContext("TestEvent"));

        await Assert.That(names).IsEquivalentTo(
            ["sub.traced-sub/TestEvent", "sub.traced-sub/OtherEvent", "sub.traced-sub/TestEvent"],
            CollectionOrdering.Matching
        );
    }

    static ActivityListener StartActivityListener(List<string> names, string prefix) {
        var listener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventuousDiagnostics.InstrumentationName,
            Sample         = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => {
                if (!activity.OperationName.StartsWith(prefix, StringComparison.Ordinal)) return;

                lock (names) names.Add(activity.OperationName);
            }
        };
        ActivitySource.AddActivityListener(listener);

        return listener;
    }

    record TestOptions : SubscriptionOptions;

    sealed class TestSubscription(ConsumePipe pipe)
        : EventSubscription<TestOptions>(new() { SubscriptionId = "traced-sub" }, pipe, NullLoggerFactory.Instance, null) {
        protected override ValueTask Connect(SubscriptionRun run) => default;

        public ValueTask Deliver(IMessageConsumeContext context) {
            context.LogContext = Log;

            return Handler(context);
        }
    }

    class TracedFirstHandler : IEventHandler {
        public string DiagnosticName => nameof(TracedFirstHandler);

        public ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) => ValueTask.FromResult(EventHandlingStatus.Success);
    }

    class TracedSecondHandler : IEventHandler {
        public string DiagnosticName => nameof(TracedSecondHandler);

        public ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) => ValueTask.FromResult(EventHandlingStatus.Success);
    }
}
