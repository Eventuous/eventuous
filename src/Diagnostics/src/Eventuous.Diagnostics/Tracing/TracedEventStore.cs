using System.Diagnostics;

// ReSharper disable InvertIf

namespace Eventuous.Diagnostics.Tracing;

public class TracedEventStore : IEventStore {
    public static IEventStore Trace(IEventStore eventStore) => new TracedEventStore(eventStore);

    public TracedEventStore(IEventStore eventStore) => Inner = eventStore;

    IEventStore Inner { get; }

    static readonly KeyValuePair<string, object?>[] DefaultTags = {
        new(TelemetryTags.Db.System, "eventstore")
    };

    public async Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken) {
        using var activity = StartActivity(stream, Constants.StreamExists);
        return await Inner.StreamExists(stream, cancellationToken).NoContext();
    }

    public async Task<AppendEventsResult> AppendEvents(
        StreamName                       stream,
        ExpectedStreamVersion            expectedVersion,
        IReadOnlyCollection<StreamEvent> events,
        CancellationToken                cancellationToken
    ) {
        using var activity = StartActivity(stream, Constants.AppendEvents);

        var tracedEvents = events.Select(
            x => x with { Metadata = x.Metadata.AddActivityTags(activity) }
        ).ToArray();

        return await Inner.AppendEvents(stream, expectedVersion, tracedEvents, cancellationToken).NoContext();
    }

    public async Task<StreamEvent[]> ReadEvents(
        StreamName         stream,
        StreamReadPosition start,
        int                count,
        CancellationToken  cancellationToken
    ) {
        using var activity = StartActivity(stream, Constants.ReadEvents);
        return await Inner.ReadEvents(stream, start, count, cancellationToken).NoContext();
    }

    public async Task<StreamEvent[]> ReadEventsBackwards(
        StreamName        stream,
        int               count,
        CancellationToken cancellationToken
    ) {
        using var activity = StartActivity(stream, Constants.ReadEvents);
        return await Inner.ReadEventsBackwards(stream, count, cancellationToken).NoContext();
    }

    public async Task<long> ReadStream(
        StreamName          stream,
        StreamReadPosition  start,
        int                 count,
        Action<StreamEvent> callback,
        CancellationToken   cancellationToken
    ) {
        using var activity = StartActivity(stream, Constants.ReadEvents);
        return await Inner.ReadStream(stream, start, count, callback, cancellationToken).NoContext();
    }

    public async Task TruncateStream(
        StreamName             stream,
        StreamTruncatePosition truncatePosition,
        ExpectedStreamVersion  expectedVersion,
        CancellationToken      cancellationToken
    ) {
        using var activity = StartActivity(stream, Constants.TruncateStream);
        await Inner.TruncateStream(stream, truncatePosition, expectedVersion, cancellationToken).NoContext();
    }

    public async Task DeleteStream(
        StreamName            stream,
        ExpectedStreamVersion expectedVersion,
        CancellationToken     cancellationToken
    ) {
        using var activity = StartActivity(stream, Constants.DeleteStream);
        await Inner.DeleteStream(stream, expectedVersion, cancellationToken).NoContext();
    }

    static Activity? StartActivity(
        StreamName       stream,
        string           operationName
    ) {
        var activity = EventuousDiagnostics.ActivitySource.CreateActivity(
            operationName,
            ActivityKind.Server,
            parentContext: default,
            DefaultTags,
            idFormat: ActivityIdFormat.W3C
        );

        if (activity is { IsAllDataRequested: true }) {
            activity.SetTag(TelemetryTags.Db.Operation, activity.OperationName);
            activity.SetTag(TelemetryTags.Eventuous.Stream, stream);
        }

        return activity?.Start();
    }
}