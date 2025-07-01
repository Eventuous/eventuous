using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Producers;
using Eventuous.TestHelpers;
using Eventuous.Tests.EventStore.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using Shouldly;

namespace Eventuous.Tests.EventStore;

public class TracesTests : LegacySubscriptionFixture<TracedHandler> {
    readonly ActivityListener _listener;

    static TracesTests() => TypeMap.Instance.AddType<TestEvent>(TestEvent.TypeName);

    public TracesTests() : base(new()) {
        _listener = new() {
            ShouldListenTo = _ => true,
            // ReSharper disable once RedundantLambdaParameterType
            Sample         = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => Log.LogTrace(
                "Started {Activity} with {Id}, parent {ParentId}",
                activity.DisplayName,
                activity.Id,
                activity.ParentId
            ),
            ActivityStopped = activity => Log.LogTrace("Stopped {Activity}", activity.DisplayName)
        };

        ActivitySource.AddActivityListener(_listener);
    }

    [Test]
    [Category("Diagnostics")]
    public async Task ShouldPropagateRemoteContext(CancellationToken cancellationToken) {
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
        recordedTrace.TraceId!.Value.ToString().ShouldBe(traceId);
        recordedTrace.ParentSpanId!.Value.ToString().ShouldBe(spanId);
    }

    [After(Test)]
    public void Dispose() => _listener.Dispose();
}
