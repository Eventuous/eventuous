using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.Producers;
using Eventuous.TestHelpers;
using Eventuous.Tests.EventStore.Subscriptions;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.EventStore;

public class TracesTests : LegacySubscriptionFixture<TracedHandler>, IDisposable {
    readonly ActivityListener _listener;

    public TracesTests(ITestOutputHelper output) : base(output, new TracedHandler(), false) {
        _listener = new ActivityListener {
            ShouldListenTo = _ => true,
            Sample         = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => Log.LogInformation(
                "Started {Activity} with {Id}, parent {ParentId}",
                activity.DisplayName,
                activity.Id,
                activity.ParentId
            ),
            ActivityStopped = activity => Log.LogInformation("Stopped {Activity}", activity.DisplayName)
        };

        ActivitySource.AddActivityListener(_listener);
    }

    [Fact]
    [Trait("Category", "Diagnostics")]
    public async Task ShouldPropagateRemoveContext() {
        var testEvent = Auto.Create<TestEvent>();

        await Producer.Produce(Stream, testEvent, new Metadata());

        await Start();

        var writtenEvent = (await StoreFixture.EventStore.ReadEvents(Stream, StreamReadPosition.Start, 1, default))[0];

        var meta = writtenEvent.Metadata;
        var (traceId, spanId, _) = meta.GetTracingMeta();

        traceId.Should().NotBe(RecordedTrace.DefaultTraceId);
        spanId.Should().NotBe(RecordedTrace.DefaultSpanId);

        while (Handler.Contexts.Count == 0) {
            await Task.Delay(100);
        }

        await Stop();

        Handler.Contexts.Should().NotBeEmpty();

        var recordedTrace = Handler.Contexts.First();

        recordedTrace.IsDefaultTraceId.Should().BeFalse();
        recordedTrace.IsDefaultSpanId.Should().BeFalse();
        recordedTrace.TraceId!.Value.ToString().Should().Be(traceId);
        recordedTrace.ParentSpanId!.Value.ToString().Should().Be(spanId);
    }

    public void Dispose() => _listener.Dispose();
}
