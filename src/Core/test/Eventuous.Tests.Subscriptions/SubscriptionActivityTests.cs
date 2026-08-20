using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.TestHelpers;

namespace Eventuous.Tests.Subscriptions;

[NotInParallel]
public class SubscriptionActivityTests : IDisposable {
    const string ActivityName    = "subscription.test/TestEvent";
    const string ForeignSource   = "some.other.instrumentation";
    const string ProducerTraceId = "0af7651916cd43dd8448eb211c80319c";
    const string ProducerSpanId  = "b7ad6b7169203331";

    readonly ActivityListener _listener;
    readonly ActivitySource   _foreignSource = new(ForeignSource);

    public SubscriptionActivityTests() {
        // This sampler deliberately does not read options.TraceId. .NET only materialises the trace id of a new
        // root when a sampler asks for it, so a sampler that reads it hides the case where an unstarted activity
        // still reports an all-zero id — which is the realistic default and the harder half of the contract.
        _listener = new() {
            ShouldListenTo = source => source.Name == EventuousDiagnostics.InstrumentationName || source.Name == ForeignSource,
            Sample         = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(_listener);
        Activity.Current = null;
    }

    static MessageConsumeContext CreateContextWithTracingMeta()
        => CreateContext(
            new Metadata()
                .With(DiagnosticTags.TraceId, ProducerTraceId)
                .With(DiagnosticTags.SpanId, ProducerSpanId)
                .With(MetaTags.CorrelationId, "correlation-1")
        );

    static MessageConsumeContext CreateContext(Metadata? metadata) {
        return new(
            Guid.NewGuid().ToString(),
            "TestEvent",
            "application/json",
            "test-stream",
            0,
            0,
            0,
            0,
            DateTime.UtcNow,
            new object(),
            metadata,
            "test-subscription",
            CancellationToken.None
        );
    }

    [Test]
    public async Task ShouldLinkToRestoredContextInsteadOfParentingToIt() {
        var context = CreateContextWithTracingMeta();

        using var activity = SubscriptionActivity.Create(ActivityName, ActivityKind.Internal, context);

        await Assert.That(activity).IsNotNull();
        await Assert.That(activity!.Parent).IsNull();
        await Assert.That(activity.ParentSpanId.ToHexString()).IsEqualTo("0000000000000000");
        await Assert.That(activity.TraceId.ToHexString()).IsNotEqualTo(ProducerTraceId);

        // Rooting the span means giving it a trace id of its own. An unstarted activity reports an all-zero id
        // unless one was materialised for it, and pinning that zero would make it permanent.
        await Assert.That(activity.TraceId.ToHexString()).IsNotEqualTo(RecordedTrace.DefaultTraceId);

        var links = activity.Links.ToArray();
        await Assert.That(links.Length).IsEqualTo(1);
        await Assert.That(links[0].Context.TraceId.ToHexString()).IsEqualTo(ProducerTraceId);
        await Assert.That(links[0].Context.SpanId.ToHexString()).IsEqualTo(ProducerSpanId);

        // Rooting the span must not drop the sampler's decision, or it would silently stop being exported.
        await Assert.That(activity.Recorded).IsTrue();
        await Assert.That(activity.IsAllDataRequested).IsTrue();
    }

    [Test]
    public async Task ShouldKeepCorrelationTagsWhenLinking() {
        var context = CreateContextWithTracingMeta();

        using var activity = SubscriptionActivity.Create(ActivityName, ActivityKind.Internal, context);

        // TagObjects, not Tags: the latter only exposes string-valued tags, and the stream is a StreamName struct.
        var tags = activity!.TagObjects.ToDictionary(x => x.Key, x => x.Value?.ToString());

        await Assert.That(tags[TelemetryTags.Messaging.MessageId]).IsEqualTo(context.MessageId);
        await Assert.That(tags[TelemetryTags.Messaging.CorrelationId]).IsEqualTo("correlation-1");
        await Assert.That(tags[TelemetryTags.Eventuous.Stream]).IsEqualTo(context.Stream.ToString());
        await Assert.That(tags[TelemetryTags.Eventuous.Subscription]).IsEqualTo(context.SubscriptionId);
    }

    [Test]
    public async Task ShouldProduceDistinctTracesOnRedelivery() {
        using var first  = SubscriptionActivity.Create(ActivityName, ActivityKind.Internal, CreateContextWithTracingMeta());
        using var second = SubscriptionActivity.Create(ActivityName, ActivityKind.Internal, CreateContextWithTracingMeta());

        await Assert.That(first!.TraceId.ToHexString()).IsNotEqualTo(second!.TraceId.ToHexString());
        await Assert.That(first.TraceId.ToHexString()).IsNotEqualTo(ProducerTraceId);
        await Assert.That(second.TraceId.ToHexString()).IsNotEqualTo(ProducerTraceId);
    }

    [Test]
    public async Task ShouldKeepTheTraceIdTheSamplerDecidedOn() {
        // A ratio-based sampler decides by reading the trace id it is offered, and .NET then materialises that id
        // on the activity. Rooting the span has to keep it, or the span carries an id the sampler never evaluated.
        _listener.Dispose();

        var sampledTraceIds = new List<string>();

        using var samplingListener = new ActivityListener {
            ShouldListenTo = source => source.Name == EventuousDiagnostics.InstrumentationName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => {
                sampledTraceIds.Add(options.TraceId.ToHexString());

                return ActivitySamplingResult.AllDataAndRecorded;
            }
        };

        ActivitySource.AddActivityListener(samplingListener);

        using var activity = SubscriptionActivity.Create(ActivityName, ActivityKind.Internal, CreateContextWithTracingMeta());

        await Assert.That(sampledTraceIds).Contains(activity!.TraceId.ToHexString());
    }

    [Test]
    public async Task ShouldParentToTheActivityStoredForTheAsyncPath() {
        // On the async handling path the subscription creates the message activity, stores it in the context items
        // and lets a filter start it further down the pipe. That stored activity is the message's own span, so it
        // stays a parent: the link belongs on it, and everything nested under it is plain in-process causality.
        using var stored = EventuousDiagnostics.ActivitySource.StartActivity("stored");
        Activity.Current = null;

        var context = CreateContextWithTracingMeta();
        context.Items.AddItem(ContextItemKeys.Activity, stored!);

        using var activity = SubscriptionActivity.Create(ActivityName, ActivityKind.Internal, context);

        await Assert.That(activity!.TraceId.ToHexString()).IsEqualTo(stored!.TraceId.ToHexString());
        await Assert.That(activity.ParentSpanId.ToHexString()).IsEqualTo(stored.SpanId.ToHexString());
        await Assert.That(activity.Links.ToArray().Length).IsEqualTo(0);
    }

    [Test]
    public async Task ShouldParentToAmbientEventuousActivity() {
        using var ambient = EventuousDiagnostics.ActivitySource.StartActivity("ambient");
        var       context = CreateContextWithTracingMeta();

        using var activity = SubscriptionActivity.Create(ActivityName, ActivityKind.Internal, context);

        await Assert.That(activity!.TraceId.ToHexString()).IsEqualTo(ambient!.TraceId.ToHexString());
        await Assert.That(activity.ParentSpanId.ToHexString()).IsEqualTo(ambient.SpanId.ToHexString());
        await Assert.That(activity.Links.ToArray().Length).IsEqualTo(0);
    }

    [Test]
    public async Task ShouldNotParentToForeignAmbientActivity() {
        using var foreign = _foreignSource.StartActivity("foreign");
        var       context = CreateContextWithTracingMeta();

        using var activity = SubscriptionActivity.Create(ActivityName, ActivityKind.Internal, context);

        await Assert.That(foreign).IsNotNull();
        await Assert.That(activity!.Parent).IsNull();
        await Assert.That(activity.TraceId.ToHexString()).IsNotEqualTo(foreign!.TraceId.ToHexString());
        await Assert.That(activity.TraceId.ToHexString()).IsNotEqualTo(ProducerTraceId);
        await Assert.That(activity.Links.ToArray().Length).IsEqualTo(1);
    }

    [Test]
    public async Task ShouldNotLinkWhenMetadataCarriesNoTracingContext() {
        // Without a restored context there is nothing to link to, so the pre-existing fallback to the ambient
        // activity must stay untouched — an all-zero link would be worse than no link.
        using var ambient = _foreignSource.StartActivity("foreign");

        using var activity = SubscriptionActivity.Create(ActivityName, ActivityKind.Internal, CreateContext(new Metadata()));

        await Assert.That(activity!.Links.ToArray().Length).IsEqualTo(0);
        await Assert.That(activity.TraceId.ToHexString()).IsEqualTo(ambient!.TraceId.ToHexString());
        await Assert.That(activity.ParentSpanId.ToHexString()).IsEqualTo(ambient.SpanId.ToHexString());
    }

    [Test]
    public async Task ShouldNotLinkWhenTracingContextIsAllZeroes() {
        using var ambient = _foreignSource.StartActivity("foreign");

        var metadata = new Metadata()
            .With(DiagnosticTags.TraceId, RecordedTrace.DefaultTraceId)
            .With(DiagnosticTags.SpanId, RecordedTrace.DefaultSpanId);

        using var activity = SubscriptionActivity.Create(ActivityName, ActivityKind.Internal, CreateContext(metadata));

        await Assert.That(activity!.Links.ToArray().Length).IsEqualTo(0);
        await Assert.That(activity.TraceId.ToHexString()).IsEqualTo(ambient!.TraceId.ToHexString());
    }

    [Test]
    public async Task ShouldNotLinkWhenThereIsNoMetadataAtAll() {
        using var ambient = _foreignSource.StartActivity("foreign");

        using var activity = SubscriptionActivity.Create(ActivityName, ActivityKind.Internal, CreateContext(null));

        await Assert.That(activity!.Links.ToArray().Length).IsEqualTo(0);
        await Assert.That(activity.TraceId.ToHexString()).IsEqualTo(ambient!.TraceId.ToHexString());
    }

    [Test]
    public async Task ShouldStayRootWhenStartedLaterUnderAmbientActivity() {
        // The async handling path creates the activity in the subscription handler and starts it further down
        // the pipe, where another activity may be ambient. A late Start must not re-parent the consume span.
        var context = CreateContextWithTracingMeta();

        using var activity = SubscriptionActivity.Create(ActivityName, ActivityKind.Internal, context);

        using var ambient = EventuousDiagnostics.ActivitySource.StartActivity("started-in-between");
        activity!.Start();

        await Assert.That(activity.Parent).IsNull();
        await Assert.That(activity.ParentSpanId.ToHexString()).IsEqualTo("0000000000000000");
        await Assert.That(activity.TraceId.ToHexString()).IsNotEqualTo(ambient!.TraceId.ToHexString());
        await Assert.That(activity.TraceId.ToHexString()).IsNotEqualTo(ProducerTraceId);
    }

    [Test]
    public async Task ShouldCarryTheNewTraceIntoWhatTheHandlerAppends() {
        // A reactor appends while consuming, and both TracedEventWriter and ProducerActivity create their span
        // with a default parent context, which makes the ambient consume span its parent. The append has to land
        // in the consume's own trace, so the producer's old trace stops propagating down the event chain.
        using var consume = SubscriptionActivity.Start(ActivityName, ActivityKind.Internal, CreateContextWithTracingMeta());

        using var append = EventuousDiagnostics.ActivitySource.CreateActivity("append", ActivityKind.Client, parentContext: default);
        append!.Start();

        await Assert.That(append.TraceId.ToHexString()).IsEqualTo(consume!.TraceId.ToHexString());
        await Assert.That(append.TraceId.ToHexString()).IsNotEqualTo(ProducerTraceId);
        await Assert.That(append.ParentSpanId.ToHexString()).IsEqualTo(consume.SpanId.ToHexString());

        // What gets stamped onto the appended event is the new trace, not the one the consumed event carried.
        var (traceId, _) = new Metadata().AddActivityTags(append).GetTracingMeta();
        await Assert.That(traceId).IsEqualTo(append.TraceId.ToHexString());
    }

    public void Dispose() {
        _listener.Dispose();
        _foreignSource.Dispose();
    }
}
