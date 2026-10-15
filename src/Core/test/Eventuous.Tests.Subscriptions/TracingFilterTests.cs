using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;

namespace Eventuous.Tests.Subscriptions;

[NotInParallel]
public class TracingFilterTests : IDisposable {
    const string TestSourceName = "eventuous.tests.tracing-filter";

    readonly ActivitySource   _testSource = new(TestSourceName);
    readonly ActivityListener _listener;

    public TracingFilterTests() {
        _listener = new() {
            ShouldListenTo = source => source.Name == EventuousDiagnostics.InstrumentationName || source.Name == TestSourceName,
            Sample         = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [Test]
    public async Task ShouldNotStopTheActivityItReuses() {
        // The subscription's own activity, which the subscription stops once it's done with the message
        using var subscriptionActivity = _testSource.StartActivity("subscription");
        await Assert.That(subscriptionActivity).IsNotNull();

        var context = TestContext.CreateContext();
        context.ParentContext = subscriptionActivity!.Context;

        var recorder = new ActivityRecorder();
        var pipe     = new ConsumePipe().AddFilterLast(new TracingFilter("test-consumer")).AddFilterLast(recorder);

        await pipe.Send(context);

        await Assert.That(recorder.Seen).IsSameReferenceAs(subscriptionActivity);
        await Assert.That(subscriptionActivity.IsStopped).IsFalse();
        await Assert.That(subscriptionActivity.Duration).IsEqualTo(TimeSpan.Zero);
    }

    [Test]
    public async Task ShouldStopTheActivityItStarts() {
        var context = TestContext.CreateContext();
        context.ParentContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);

        var recorder = new ActivityRecorder();
        var pipe     = new ConsumePipe().AddFilterLast(new TracingFilter("test-consumer")).AddFilterLast(recorder);

        await pipe.Send(context);

        await Assert.That(recorder.Seen).IsNotNull();
        await Assert.That(recorder.Seen!.Source.Name).IsEqualTo(EventuousDiagnostics.InstrumentationName);
        await Assert.That(recorder.Seen.IsStopped).IsTrue();
    }

    [Test]
    public async Task ShouldKeepTheErrorStatusOfAFailedMessage() {
        using var subscriptionActivity = _testSource.StartActivity("subscription");
        await Assert.That(subscriptionActivity).IsNotNull();

        var context = TestContext.CreateContext();
        context.ParentContext = subscriptionActivity!.Context;

        var pipe = new ConsumePipe().AddFilterLast(new TracingFilter("test-consumer")).AddFilterLast(new NackingFilter());

        await pipe.Send(context);

        await Assert.That(context.HasFailed()).IsTrue();
        await Assert.That(subscriptionActivity.Status).IsEqualTo(ActivityStatusCode.Error);
    }

    /// <summary>
    /// Terminal filter recording the activity the handler would run under.
    /// </summary>
    sealed class ActivityRecorder : ConsumeFilter<IMessageConsumeContext> {
        public Activity? Seen { get; private set; }

        protected override ValueTask Send(IMessageConsumeContext context, LinkedListNode<IConsumeFilter>? next) {
            Seen = Activity.Current;

            return default;
        }
    }

    /// <summary>
    /// Terminal filter failing the message the way the default consumer does: with Nack, not by throwing.
    /// </summary>
    sealed class NackingFilter : ConsumeFilter<IMessageConsumeContext> {
        protected override ValueTask Send(IMessageConsumeContext context, LinkedListNode<IConsumeFilter>? next) {
            context.Nack("test-handler", new InvalidOperationException("handler failed"));

            return default;
        }
    }

    public void Dispose() {
        _listener.Dispose();
        _testSource.Dispose();
    }
}
