using System.Diagnostics;

// ReSharper disable InvertIf

namespace Eventuous.Diagnostics.Tracing;

public class TracedEventStore : IEventStore {
    public static IEventStore Trace(IEventStore eventStore) => new TracedEventStore(eventStore);

    TracedEventStore(IEventStore eventStore) => Inner = eventStore;

    IEventStore Inner { get; }

    static readonly KeyValuePair<string, object?>[] DefaultTags = {
        new(TelemetryTags.Db.System, "eventstore")
    };

    public async Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken) {
        using var activity = CreateActivity(stream, "stream-exists");
        return await Inner.StreamExists(stream, cancellationToken);
    }

    public async Task<AppendEventsResult> AppendEvents(
        StreamName                       stream,
        ExpectedStreamVersion            expectedVersion,
        IReadOnlyCollection<StreamEvent> events,
        CancellationToken                cancellationToken
    ) {
        using var activity = CreateActivity(stream, "append-events");

        var tracedEvents = events.Select(
            x => x with { Metadata = x.Metadata.AddActivityTags(activity) }
        ).ToArray();

        return await Inner.AppendEvents(stream, expectedVersion, tracedEvents, cancellationToken);
    }

    public async Task<StreamEvent[]> ReadEvents(
        StreamName         stream,
        StreamReadPosition start,
        int                count,
        CancellationToken  cancellationToken
    ) {
        using var activity = CreateActivity(stream, "read-events");
        return await Inner.ReadEvents(stream, start, count, cancellationToken);
    }

    public async Task<StreamEvent[]> ReadEventsBackwards(
        StreamName        stream,
        int               count,
        CancellationToken cancellationToken
    ) {
        using var activity = CreateActivity(stream, "read-events");
        return await Inner.ReadEventsBackwards(stream, count, cancellationToken);
    }

    public async Task<long> ReadStream(
        StreamName          stream,
        StreamReadPosition  start,
        int                 count,
        Action<StreamEvent> callback,
        CancellationToken   cancellationToken
    ) {
        using var activity = CreateActivity(stream, "read-events");
        return await Inner.ReadStream(stream, start, count, callback, cancellationToken);
    }

    public async Task TruncateStream(
        StreamName             stream,
        StreamTruncatePosition truncatePosition,
        ExpectedStreamVersion  expectedVersion,
        CancellationToken      cancellationToken
    ) {
        using var activity = CreateActivity(stream, "truncate-stream");
        await Inner.TruncateStream(stream, truncatePosition, expectedVersion, cancellationToken);
    }

    public async Task DeleteStream(
        StreamName            stream,
        ExpectedStreamVersion expectedVersion,
        CancellationToken     cancellationToken
    ) {
        using var activity = CreateActivity(stream, "delete-stream");
        await Inner.DeleteStream(stream, expectedVersion, cancellationToken);
    }

    static Activity? CreateActivity(
        StreamName       stream,
        string           operationName
    ) {
        var activity = EventuousDiagnostics.ActivitySource.CreateActivity(
            operationName,
            ActivityKind.Server,
            parentContext: default,
            DefaultTags
        );

        if (activity is { IsAllDataRequested: true }) {
            activity.SetTag(TelemetryTags.Db.Operation, activity.OperationName);
            activity.SetTag(TelemetryTags.EventStore.Stream, stream);
        }

        return activity;
    }
}