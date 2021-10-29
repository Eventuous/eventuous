// ReSharper disable CoVariantArrayConversion

using System.Diagnostics;
using Eventuous.Diagnostics;

namespace Eventuous.EventStore;

[PublicAPI]
public class EsdbEventStore : IEventStore {
    readonly ILogger<EsdbEventStore>? _logger;
    readonly EventStoreClient         _client;

    public EsdbEventStore(EventStoreClient client, ILogger<EsdbEventStore>? logger) {
        _logger = logger;
        _client = Ensure.NotNull(client, nameof(client));
    }

    public EsdbEventStore(EventStoreClientSettings clientSettings, ILogger<EsdbEventStore>? logger)
        : this(new EventStoreClient(Ensure.NotNull(clientSettings, nameof(clientSettings))), logger) { }
    
    static readonly KeyValuePair<string, object?>[] DefaultTags = {
        new(TelemetryTags.Db.System, "eventstoredb")
    };

    public async Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken) {
        using var activity = SharedDiagnostics.ActivitySource.CreateActivity(
            "stream-exists",
            ActivityKind.Internal,
            parentContext: default,
            DefaultTags
        );
        
        if (activity is { IsAllDataRequested: true }) {
            activity.SetTag(TelemetryTags.Db.Operation, "read-stream");
            activity.SetTag(TelemetryTags.EventStore.Stream, stream);
        }
        
        var read = _client.ReadStreamAsync(
            Direction.Backwards,
            stream,
            StreamPosition.End,
            1,
            cancellationToken: cancellationToken
        );

        var state = await read.ReadState;
        return state == ReadState.Ok;
    }

    public Task<AppendEventsResult> AppendEvents(
        StreamName                       stream,
        ExpectedStreamVersion            expectedVersion,
        IReadOnlyCollection<StreamEvent> events,
        CancellationToken                cancellationToken
    ) {
        var proposedEvents = events.Select(ToEventData);

        var resultTask = expectedVersion == ExpectedStreamVersion.NoStream
            ? _client.AppendToStreamAsync(
                stream,
                StreamState.NoStream,
                proposedEvents,
                cancellationToken: cancellationToken
            ) : AnyOrNot(
                expectedVersion,
                () => _client.AppendToStreamAsync(
                    stream,
                    StreamState.Any,
                    proposedEvents,
                    cancellationToken: cancellationToken
                ),
                () => _client.AppendToStreamAsync(
                    stream,
                    expectedVersion.AsStreamRevision(),
                    proposedEvents,
                    cancellationToken: cancellationToken
                )
            );

        return TryExecute(
            async () => {
                var result = await resultTask.NoContext();

                return new AppendEventsResult(
                    result.LogPosition.CommitPosition,
                    result.NextExpectedStreamRevision.ToInt64()
                );
            },
            stream,
            () => new ErrorInfo("Unable to appends events to {Stream}", stream),
            (s, ex) => new AppendToStreamException(s, ex)
        );

        static EventData ToEventData(StreamEvent streamEvent)
            => new(
                Uuid.NewUuid(),
                streamEvent.EventType,
                streamEvent.Data,
                streamEvent.Metadata
            );
    }

    public Task<StreamEvent[]> ReadEvents(
        StreamName         stream,
        StreamReadPosition start,
        int                count,
        CancellationToken  cancellationToken
    ) {
        var read = _client.ReadStreamAsync(
            Direction.Forwards,
            stream,
            start.AsStreamPosition(),
            count,
            cancellationToken: cancellationToken
        );

        return TryExecute(
            async () => {
                var resolvedEvents = await read.ToArrayAsync(cancellationToken).NoContext();
                return ToStreamEvents(resolvedEvents);
            },
            stream,
            () => new ErrorInfo(
                "Unable to read {Count} starting at {Start} events from {Stream}",
                count,
                start,
                stream
            ),
            (s, ex) => new ReadFromStreamException(s, ex)
        );
    }

    public Task<StreamEvent[]> ReadEventsBackwards(
        StreamName        stream,
        int               count,
        CancellationToken cancellationToken
    ) {
        var read = _client.ReadStreamAsync(
            Direction.Backwards,
            stream,
            StreamPosition.End,
            count,
            cancellationToken: cancellationToken
        );

        return TryExecute(
            async () => {
                var resolvedEvents = await read.ToArrayAsync(cancellationToken).NoContext();
                return ToStreamEvents(resolvedEvents);
            },
            stream,
            () => new ErrorInfo("Unable to read {Count} events backwards from {Stream}", count, stream),
            (s, ex) => new ReadFromStreamException(s, ex)
        );
    }

    public async Task<long> ReadStream(
        StreamName          stream,
        StreamReadPosition  start,
        int                 count,
        Action<StreamEvent> callback,
        CancellationToken   cancellationToken
    ) {
        var read = _client.ReadStreamAsync(
            Direction.Forwards,
            stream,
            start.AsStreamPosition(),
            count,
            cancellationToken: cancellationToken
        );

        return await TryExecute(
            async () => {
                long readCount = 0;
                await foreach (var re in read.IgnoreWithCancellation(cancellationToken)) {
                    callback(ToStreamEvent(re));
                    readCount++;
                }

                return readCount;
            },
            stream,
            () => new ErrorInfo("Unable to read stream {Stream} from {Start}", stream, start),
            (s, ex) => new ReadFromStreamException(s, ex)
        );
    }

    public Task TruncateStream(
        StreamName             stream,
        StreamTruncatePosition truncatePosition,
        ExpectedStreamVersion  expectedVersion,
        CancellationToken      cancellationToken
    ) {
        var meta = new StreamMetadata(truncateBefore: truncatePosition.AsStreamPosition());

        return TryExecute(
            () => AnyOrNot(
                expectedVersion,
                () => _client.SetStreamMetadataAsync(
                    stream,
                    StreamState.Any,
                    meta,
                    cancellationToken: cancellationToken
                ),
                () => _client.SetStreamMetadataAsync(
                    stream,
                    expectedVersion.AsStreamRevision(),
                    meta,
                    cancellationToken: cancellationToken
                )
            ),
            stream,
            () => new ErrorInfo("Unable to truncate stream {Stream} at {Position}", stream, truncatePosition),
            (s, ex) => new TruncateStreamException(s, ex)
        );
    }

    public Task DeleteStream(
        StreamName            stream,
        ExpectedStreamVersion expectedVersion,
        CancellationToken     cancellationToken
    ) => TryExecute(
        () => AnyOrNot(
            expectedVersion,
            () => _client.SoftDeleteAsync(
                stream,
                StreamState.Any,
                cancellationToken: cancellationToken
            ),
            () => _client.SoftDeleteAsync(
                stream,
                expectedVersion.AsStreamRevision(),
                cancellationToken: cancellationToken
            )
        ),
        stream,
        () => new ErrorInfo("Unable to delete stream {Stream}", stream),
        (s, ex) => new DeleteStreamException(s, ex)
    );

    async Task<T> TryExecute<T>(
        Func<Task<T>>                      func,
        string                             stream,
        Func<ErrorInfo>                    getError,
        Func<string, Exception, Exception> getException
    ) {
        try {
            return await func();
        }
        catch (StreamNotFoundException) {
            _logger?.LogWarning("Stream {Stream} not found", stream);
            throw new StreamNotFound(stream);
        }
        catch (Exception ex) {
            var (message, args) = getError();
            _logger?.LogWarning(ex, message, args);
            throw getException(stream, ex);
        }
    }

    static Task<T> AnyOrNot<T>(
        ExpectedStreamVersion version,
        Func<Task<T>>         whenAny,
        Func<Task<T>>         otherwise
    )
        => version == ExpectedStreamVersion.Any ? whenAny() : otherwise();

    static StreamEvent ToStreamEvent(ResolvedEvent resolvedEvent)
        => new(
            resolvedEvent.Event.EventType,
            resolvedEvent.Event.Data.ToArray(),
            resolvedEvent.Event.Metadata.ToArray(),
            resolvedEvent.Event.ContentType,
            resolvedEvent.OriginalEventNumber.ToInt64()
        );

    static StreamEvent[] ToStreamEvents(ResolvedEvent[] resolvedEvents)
        => resolvedEvents.Select(ToStreamEvent).ToArray();

    record ErrorInfo(string Message, params object[] Args);
}