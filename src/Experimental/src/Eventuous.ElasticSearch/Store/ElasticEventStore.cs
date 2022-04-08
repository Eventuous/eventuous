using Elasticsearch.Net;
using Nest;

namespace Eventuous.ElasticSearch.Store;

public class ElasticEventStore : IEventStore {
    readonly IElasticClient _client;

    public ElasticEventStore(IElasticClient client) => _client = client;

    public async Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken) {
        var response = await _client.SearchAsync<PersistedEvent>(
            d => d.Index("eventuous").Query(q => q.Term(x => x.Stream, stream.ToString())).Size(1),
            cancellationToken
        );

        return response.IsValid && response.Documents.Count > 0;
    }

    public async Task<AppendEventsResult> AppendEvents(
        StreamName                       stream,
        ExpectedStreamVersion            expectedVersion,
        IReadOnlyCollection<StreamEvent> events,
        CancellationToken                cancellationToken
    ) {
        var streamName = stream.ToString();
        var documents  = events.Select(AsDocument).ToArray();
        var bulk       = new BulkDescriptor("eventuous").CreateMany(documents).Refresh(Refresh.WaitFor);
        var result     = await _client.BulkAsync(bulk, cancellationToken);

        return result.IsValid
            ? new AppendEventsResult(0, documents.Last().StreamPosition + 1)
            : throw new ApplicationException("Unable to add events");

        PersistedEvent AsDocument(StreamEvent evt)
            => new(
                Guid.NewGuid().ToString("N"),
                VersionParser.Parse(TypeMap.Instance.GetTypeName(evt.Payload!)),
                evt.Position + 1,
                evt.ContentType,
                streamName,
                (ulong)evt.Position + 1,
                evt.Payload,
                evt.Metadata.ToHeaders(),
                DateTime.Now
            );
    }

    public async Task<StreamEvent[]> ReadEvents(
        StreamName         stream,
        StreamReadPosition start,
        int                count,
        CancellationToken  cancellationToken
    ) {
        var response = await _client.SearchAsync<PersistedEvent>(
            d => d
                .Index("eventuous")
                .Query(
                    q => q
                        .Term(x => x.Stream, stream.ToString())
                )
                .Skip((int?)start.Value)
                .Size(count),
            cancellationToken
        );

        return response.Documents
            .Select(
                x => new StreamEvent(x.Message, Metadata.FromHeaders(x.Metadata), x.ContentType, x.StreamPosition)
            )
            .ToArray();
    }

    public Task<StreamEvent[]> ReadEventsBackwards(StreamName stream, int count, CancellationToken cancellationToken)
        => throw new NotImplementedException();

    public async Task<long> ReadStream(
        StreamName          stream,
        StreamReadPosition  start,
        int                 count,
        Action<StreamEvent> callback,
        CancellationToken   cancellationToken
    ) {
        var events = await ReadEvents(stream, start, count, cancellationToken);

        foreach (var streamEvent in events) {
            callback(streamEvent);
        }

        return events.Length;
    }

    public Task TruncateStream(
        StreamName             stream,
        StreamTruncatePosition truncatePosition,
        ExpectedStreamVersion  expectedVersion,
        CancellationToken      cancellationToken
    )
        => throw new NotImplementedException();

    public Task DeleteStream(
        StreamName            stream,
        ExpectedStreamVersion expectedVersion,
        CancellationToken     cancellationToken
    )
        => throw new NotImplementedException();
}
