using System.Diagnostics;
using static Eventuous.Diagnostics.Tracing.Constants;

// ReSharper disable InvertIf

namespace Eventuous.Diagnostics.Tracing;

public class TracedEventStore : IEventStore {
    public static IEventStore Trace(IEventStore eventStore) => new TracedEventStore(eventStore);

    public TracedEventStore(IEventStore eventStore) => Inner = eventStore;

    IEventStore Inner { get; }

    static readonly KeyValuePair<string, object?>[] DefaultTags = EventuousDiagnostics.Tags
        .Concat(new KeyValuePair<string, object?>[] { new(TelemetryTags.Db.System, "eventstore") })
        .ToArray();

    public Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken)
        => Trace(stream, Operations.StreamExists, () => Inner.StreamExists(stream, cancellationToken));

    public async Task<AppendEventsResult> AppendEvents(
        StreamName                       stream,
        ExpectedStreamVersion            expectedVersion,
        IReadOnlyCollection<StreamEvent> events,
        CancellationToken                cancellationToken
    ) {
        using var activity = StartActivity(stream, Operations.AppendEvents);

        var tracedEvents = events.Select(
                x => x with { Metadata = x.Metadata.AddActivityTags(activity) }
            )
            .ToArray();

        try {
            var result = await Inner.AppendEvents(stream, expectedVersion, tracedEvents, cancellationToken).NoContext();
            activity?.SetActivityStatus(ActivityStatus.Ok());
            return result;
        }
        catch (Exception e) {
            activity?.SetActivityStatus(ActivityStatus.Error(e));
            throw;
        }
    }

    public Task<StreamEvent[]> ReadEvents(
        StreamName         stream,
        StreamReadPosition start,
        int                count,
        CancellationToken  cancellationToken
    )
        => Trace(stream, Operations.ReadEvents, () => Inner.ReadEvents(stream, start, count, cancellationToken));

    public Task TruncateStream(
        StreamName             stream,
        StreamTruncatePosition truncatePosition,
        ExpectedStreamVersion  expectedVersion,
        CancellationToken      cancellationToken
    )
        => Trace(
            stream,
            Operations.TruncateStream,
            () => Inner.TruncateStream(stream, truncatePosition, expectedVersion, cancellationToken)
        );

    public Task DeleteStream(
        StreamName            stream,
        ExpectedStreamVersion expectedVersion,
        CancellationToken     cancellationToken
    )
        => Trace(stream, Operations.DeleteStream, () => Inner.DeleteStream(stream, expectedVersion, cancellationToken));

    static async Task Trace(StreamName stream, string operation, Func<Task> task) {
        using var activity = StartActivity(stream, operation);

        try {
            await task().NoContext();
            activity?.SetActivityStatus(ActivityStatus.Ok());
        }
        catch (Exception e) {
            activity?.SetActivityStatus(ActivityStatus.Error(e));
            throw;
        }
    }

    static async Task<T> Trace<T>(StreamName stream, string operation, Func<Task<T>> task) {
        using var activity = StartActivity(stream, operation);

        try {
            var result = await task().NoContext();
            activity?.SetActivityStatus(ActivityStatus.Ok());
            return result;
        }
        catch (Exception e) {
            activity?.SetActivityStatus(ActivityStatus.Error(e));
            throw;
        }
    }

    static Activity? StartActivity(StreamName stream, string operationName) {
        var streamName = stream.ToString();

        var activity = EventuousDiagnostics.ActivitySource.CreateActivity(
            $"{Components.EventStore}.{operationName}/{streamName}",
            ActivityKind.Server,
            parentContext: default,
            DefaultTags,
            idFormat: ActivityIdFormat.W3C
        );

        if (activity is { IsAllDataRequested: true }) {
            activity.SetTag(TelemetryTags.Db.Operation, operationName);
            activity.SetTag(TelemetryTags.Eventuous.Stream, streamName);
        }

        return activity?.Start();
    }
}
