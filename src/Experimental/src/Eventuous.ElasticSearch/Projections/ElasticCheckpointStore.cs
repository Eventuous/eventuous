// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using Elasticsearch.Net;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Logging;
using Microsoft.Extensions.Options;
using Nest;

namespace Eventuous.ElasticSearch.Projections;

public class ElasticCheckpointStore : ICheckpointStore {
    readonly IElasticClient _client;
    readonly int            _batchSize;

    public ElasticCheckpointStore(IElasticClient client, ElasticCheckpointStoreOptions options) {
        _client    = client;
        _batchSize = options.BatchSize;
        CreateCheckpointIndex(client, options.IndexName);
    }

    public ElasticCheckpointStore(IElasticClient client) : this(client, new ElasticCheckpointStoreOptions()) { }

    public ElasticCheckpointStore(IElasticClient client, IOptions<ElasticCheckpointStoreOptions> options)
        : this(client, options.Value) { }

    static void CreateCheckpointIndex(IElasticClient client, string indexName) {
        var exists = client.Indices.Exists(indexName).Exists;

        if (!exists) client.Indices.Create(indexName, create => create.Map<Checkpoint>(map => map.AutoMap()));

        client.ConnectionSettings.DefaultIndices.TryAdd(typeof(Checkpoint), indexName);
    }

    readonly ConcurrentDictionary<string, int> _counters = new();

    public async ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken) {
        var response = await _client.GetAsync(
                DocumentPath<Checkpoint>.Id(checkpointId),
                x => x.Realtime(),
                cancellationToken
            )
            .NoContext();

        var checkpoint = response?.Source ?? new Checkpoint(checkpointId, null);
        _counters[checkpointId] = 0;

        Logger.Current.CheckpointLoaded(this, checkpoint);

        return checkpoint;
    }

    public async ValueTask<Checkpoint> StoreCheckpoint(
        Checkpoint        checkpoint,
        bool              force,
        CancellationToken cancellationToken
    ) {
        _counters[checkpoint.Id]++;
        if (!force && _counters[checkpoint.Id] < _batchSize) return checkpoint;

        var response = await _client.UpdateAsync(
                DocumentPath<Checkpoint>.Id(checkpoint.Id),
                x => x
                    .Doc(checkpoint)
                    .DocAsUpsert()
                    .SourceEnabled(false)
                    .RetryOnConflict(3)
                    .Refresh(Refresh.False),
                cancellationToken
            )
            .NoContext();

        if (response.OriginalException != null) throw response.OriginalException;

        _counters[checkpoint.Id] = 0;

        Logger.Current.CheckpointStored(this, checkpoint, force);

        return checkpoint;
    }
}

[PublicAPI]
public record ElasticCheckpointStoreOptions {
    public string IndexName { get; init; } = "checkpoint";
    public int    BatchSize { get; init; }
}
