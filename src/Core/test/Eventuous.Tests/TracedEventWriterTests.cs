using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.Diagnostics;
using Eventuous.Diagnostics.Tracing;

namespace Eventuous.Tests;

[NotInParallel]
public class TracedEventWriterTests : IDisposable {
    readonly ActivityListener _listener = new() {
        ShouldListenTo = source => source.Name == EventuousDiagnostics.InstrumentationName,
        Sample         = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
    };

    public TracedEventWriterTests() {
        ActivitySource.AddActivityListener(_listener);
        Activity.Current = null;
    }

    [Test]
    public async Task ShouldPassASnapshotOfTheEventsEnrichedWithTracingMeta(CancellationToken cancellationToken) {
        var inner  = new CapturingWriter();
        var writer = TracedEventWriter.Trace(inner);
        var events = new[] { new NewStreamEvent(Guid.NewGuid(), new object(), new()) };

        await writer.AppendEvents(new("stream-1"), ExpectedStreamVersion.Any, events, cancellationToken);

        await Assert.That(inner.Received).IsNotSameReferenceAs(events);
        await Assert.That(inner.Received!.Single().Id).IsEqualTo(events[0].Id);
        await Assert.That(inner.Received!.Single().Metadata.GetTracingMeta().TraceId).IsNotNull();
    }

    [Test]
    public async Task ShouldPassASnapshotOfTheAppendsEnrichedWithTracingMeta(CancellationToken cancellationToken) {
        var inner   = new CapturingWriter();
        var writer  = TracedEventWriter.Trace(inner);
        var events  = new[] { new NewStreamEvent(Guid.NewGuid(), new object(), new()) };
        var appends = new[] { new NewStreamAppend(new("stream-1"), ExpectedStreamVersion.Any, events) };

        await writer.AppendEvents(appends, cancellationToken);

        await Assert.That(inner.ReceivedAppends).IsNotSameReferenceAs(appends);
        var received = inner.ReceivedAppends!.Single().Events;
        await Assert.That(received).IsNotSameReferenceAs(events);
        await Assert.That(received.Single().Id).IsEqualTo(events[0].Id);
        await Assert.That(received.Single().Metadata.GetTracingMeta().TraceId).IsNotNull();
    }

    [Test]
    public async Task ShouldMeasureBothAppendsWhenMetricsAreObserved(CancellationToken cancellationToken) {
        var components = new List<object?>();

        using var meterListener = new MeterListener {
            InstrumentPublished = (instrument, listener) => {
                if (instrument.Name == PersistenceMetrics.ProcessingRateName) listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<double>(
            (_, _, tags, _) => {
                foreach (var tag in tags) {
                    if (tag.Key != "component") continue;

                    lock (components) components.Add(tag.Value);
                }
            }
        );
        meterListener.Start();

        using var metrics = new PersistenceMetrics();

        var writer = TracedEventWriter.Trace(new CapturingWriter());
        var events = new[] { new NewStreamEvent(Guid.NewGuid(), new object(), new()) };

        await writer.AppendEvents(new("stream-1"), ExpectedStreamVersion.Any, events, cancellationToken);
        await writer.AppendEvents([new NewStreamAppend(new("stream-1"), ExpectedStreamVersion.Any, events)], cancellationToken);

        await Assert.That(components.Count(x => Equals(x, nameof(CapturingWriter)))).IsEqualTo(2);
    }

    public void Dispose() => _listener.Dispose();

    class CapturingWriter : IEventWriter {
        public IReadOnlyCollection<NewStreamEvent>? Received        { get; private set; }
        public IReadOnlyCollection<NewStreamAppend>? ReceivedAppends { get; private set; }

        public Task<AppendEventsResult> AppendEvents(
                StreamName                          stream,
                ExpectedStreamVersion               expectedVersion,
                IReadOnlyCollection<NewStreamEvent> events,
                CancellationToken                   cancellationToken
            ) {
            Received = events;

            return Task.FromResult(new AppendEventsResult(0, 0));
        }

        public Task<AppendEventsResult[]> AppendEvents(IReadOnlyCollection<NewStreamAppend> appends, CancellationToken cancellationToken) {
            ReceivedAppends = appends;

            return Task.FromResult<AppendEventsResult[]>([new(0, 0)]);
        }
    }
}
