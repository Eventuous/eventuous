// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.ElasticSearch.Store;

public class ElasticEventStore(IElasticClient client, ElasticEventStoreOptions? options = null) : IEventStore {
    readonly ElasticEventStoreOptions _options = options ?? new ElasticEventStoreOptions();

    public async Task<AppendEventsResult> AppendEvents(
            StreamName                          stream,
            ExpectedStreamVersion               expectedVersion,
            IReadOnlyCollection<NewStreamEvent> events,
            CancellationToken                   cancellationToken
        ) {
        var streamName = stream.ToString();
        var documents  = events.Select((@event, i) => AsDocument(@event, i + expectedVersion.Value)).ToArray();
        var bulk       = new BulkDescriptor(_options.IndexName).CreateMany(documents).Refresh(Refresh.WaitFor);
        var result     = await client.BulkAsync(bulk, cancellationToken);

        return result.IsValid
            ? new AppendEventsResult(0, documents.Last().StreamPosition + 1)
            : throw new ApplicationException($"Unable to add events: {result.DebugInformation}");

        PersistedEvent AsDocument(NewStreamEvent evt, long position)
            => new(
                evt.Id.ToString(),
                TypeMap.Instance.GetTypeName(evt.Payload!),
                position + 1,
                "application/json",
                streamName,
                (ulong)position + 1,
                evt.Payload,
                evt.Metadata.ToHeaders(),
                DateTime.Now
            );
    }

    public async Task<StreamEvent[]> ReadEvents(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken) {
        var response = await ReadEvents(
            q => q.Bool(
                b => b.Must(
                    mu => mu.Term(x => x.Stream, stream.ToString()),
                    mu => mu.Range(r => r.Field(x => x.StreamPosition).GreaterThanOrEquals(start.Value))
                )
            ),
            count,
            cancellationToken
        );

        return response.Length == 0 ? throw new StreamNotFound(stream) : response;
    }

    public async Task<StreamEvent[]> ReadEventsBackwards(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken) {
        var response = await ReadEvents(
            q => q.Bool(
                b => b.Must(
                    mu => mu.Term(x => x.Stream, stream.ToString()),
                    mu => mu.Range(r => r.Field(x => x.StreamPosition).LessThanOrEquals(start.Value))
                )
            ),
            count,
            cancellationToken
        );

        return response.Length == 0 ? throw new StreamNotFound(stream) : response.OrderByDescending(x => x.Position).ToArray();
    }

    public async Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken) {
        var response = await ReadEvents(
            q => q.Bool(b => b.Must(mu => mu.Term(x => x.Stream, stream.ToString()))),
            1,
            cancellationToken
        );

        return response.Length == 1;
    }

    async Task<StreamEvent[]> ReadEvents(Func<QueryContainerDescriptor<PersistedEvent>, QueryContainer> query, int count, CancellationToken cancellationToken) {
        var response = await client.SearchAsync<PersistedEvent>(d => d.Index(_options.IndexName).Query(query).Take(count), cancellationToken);

        if (!response.IsValid) throw new ApplicationException($"Unable to read events: {response.DebugInformation}");

        return response.Documents
            .Select(
                x => new StreamEvent(
                    Guid.Parse(x.MessageId),
                    x.Message,
                    Metadata.FromHeaders(x.Metadata),
                    x.ContentType,
                    x.StreamPosition
                )
            )
            .ToArray();
    }

    public Task TruncateStream(
            StreamName             stream,
            StreamTruncatePosition truncatePosition,
            ExpectedStreamVersion  expectedVersion,
            CancellationToken      cancellationToken
        ) => throw new NotImplementedException();

    public Task DeleteStream(StreamName stream, ExpectedStreamVersion expectedVersion, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}

public record ElasticEventStoreOptions {
    public string IndexName { get; init; } = "eventlog";
}
