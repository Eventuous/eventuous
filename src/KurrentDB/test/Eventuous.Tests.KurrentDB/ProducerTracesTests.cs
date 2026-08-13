using System.Collections.Concurrent;
using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Producers;
using Eventuous.TestHelpers;
using Eventuous.Tests.KurrentDB.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using Shouldly;
using Constants = Eventuous.Diagnostics.Tracing.Constants;

namespace Eventuous.Tests.KurrentDB;

public class TracesTests : LegacySubscriptionFixture<TracedHandler> {
    readonly ActivityListener        _listener;
    readonly ConcurrentBag<Activity> _startedActivities = [];

    static TracesTests() => TypeMap.Instance.AddType<TestEvent>(TestEvent.TypeName);

    public TracesTests() : base(new()) {
        _listener = new() {
            ShouldListenTo = _ => true,
            // ReSharper disable once RedundantLambdaParameterType
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => {
                _startedActivities.Add(activity);

                Log.LogTrace("Started {Activity} with {Id}, parent {ParentId}", activity.DisplayName, activity.Id, activity.ParentId);
            },
            ActivityStopped = activity => Log.LogTrace("Stopped {Activity}", activity.DisplayName)
        };

        ActivitySource.AddActivityListener(_listener);
    }

    [Test]
    [Category("Diagnostics")]
    public async Task ShouldLinkToRemoteContextWithoutJoiningItsTrace(CancellationToken cancellationToken) {
        var testEvent = TestEvent.Create();

        await Producer.Produce(Stream, testEvent, new(), cancellationToken: cancellationToken);

        await Start();

        var writtenEvent = (await StoreFixture.EventStore.ReadEvents(Stream, StreamReadPosition.Start, 1, true, cancellationToken))[0];

        var meta = writtenEvent.Metadata;
        var (traceId, spanId) = meta.GetTracingMeta();

        traceId.ShouldNotBe(RecordedTrace.DefaultTraceId);
        spanId.ShouldNotBe(RecordedTrace.DefaultSpanId);

        while (Handler.Contexts.Count == 0) {
            await Task.Delay(100, cancellationToken);
        }

        await Stop();

        Handler.Contexts.ShouldNotBeEmpty();

        var recordedTrace = Handler.Contexts.First();

        recordedTrace.IsDefaultTraceId.ShouldBeFalse();
        recordedTrace.IsDefaultSpanId.ShouldBeFalse();

        // The consume span roots its own trace and links back to the producer, so that redelivering a stored
        // event can never keep growing the trace the producer started.
        recordedTrace.TraceId!.Value.ToString().ShouldNotBe(traceId);

        // The link sits on the subscription activity, which restored the context. It isn't reachable from the
        // handler's ambient activity on the async consume path, so it's asserted on the recorded activity instead.
        // The listener is process-wide, so the activity is matched on this subscription's own id to keep any
        // test running in parallel out of the result.
        var subscriptionActivity = _startedActivities
            .Single(x => x.OperationName.StartsWith($"{Constants.Components.Subscription}.{Subscription.SubscriptionId}/", StringComparison.Ordinal));

        subscriptionActivity.TraceId.ToString().ShouldNotBe(traceId);
        subscriptionActivity.Parent.ShouldBeNull();

        var link = subscriptionActivity.Links.ShouldHaveSingleItem();
        link.Context.TraceId.ToString().ShouldBe(traceId);
        link.Context.SpanId.ToString().ShouldBe(spanId);
    }

    [After(Test)]
    public void Dispose() => _listener.Dispose();
}
