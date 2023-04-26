// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.ElasticSearch.Store;

public class ElasticEventStore : IEventReader, IEventWriter {
    readonly IElasticClient           _client;
    readonly ElasticEventStoreOptions _options;

    public ElasticEventStore(IElasticClient client, ElasticEventStoreOptions? options = null) {
        _client  = client;
        _options = options ?? new ElasticEventStoreOptions();
    }

    public async Task<AppendEventsResult> AppendEvents(
        StreamName                       stream,
        ExpectedStreamVersion            expectedVersion,
        IReadOnlyCollection<StreamEvent> events,
        CancellationToken                cancellationToken
    ) {
        var streamName = stream.ToString();
        var documents  = events.Select(AsDocument).ToArray();
        var bulk       = new BulkDescriptor(_options.IndexName).CreateMany(documents).Refresh(Refresh.WaitFor);
        var result     = await _client.BulkAsync(bulk, cancellationToken);

        return result.IsValid
            ? new AppendEventsResult(0, documents.Last().StreamPosition + 1)
            : throw new ApplicationException($"Unable to add events: {result.DebugInformation}");

        PersistedEvent AsDocument(StreamEvent evt)
            => new(
                evt.Id.ToString(),
                TypeMap.Instance.GetTypeName(evt.Payload!),
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
                .Index(_options.IndexName)
                .Query(
                    q => q
                        .Bool(
                            b => b
                                .Must(
                                    mu => mu
                                        .Term(
                                            x => x.Stream,
                                            stream.ToString()
                                        ),
                                    mu => mu
                                        .Range(
                                            r => r
                                                .Field(x => x.StreamPosition)
                                                .GreaterThanOrEquals(start.Value)
                                        )
                                )
                        )
                )
                .Take(count),
            cancellationToken
        );

        if (!response.IsValid) throw new ApplicationException($"Unable to read events: {response.DebugInformation}");

        return response.Documents.Count == 0
            ? throw new StreamNotFound(stream)
            : response.Documents
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

    public Task<StreamEvent[]> ReadEventsBackwards(StreamName stream, int count, CancellationToken cancellationToken)
        => throw new NotImplementedException();
}

public record ElasticEventStoreOptions {
    public string IndexName { get; init; } = "eventlog";
}