using System.Collections.Concurrent;
using Eventuous.Subscriptions.Checkpoints;
using Microsoft.Extensions.Options;
using MongoDB.Driver.Linq;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;
using MongoDefaults = Eventuous.Projections.MongoDB.Tools.MongoDefaults;

namespace Eventuous.Projections.MongoDB;

[PublicAPI]
public class MongoCheckpointStore : ICheckpointStore {
    readonly int _batchSize;

    MongoCheckpointStore(IMongoDatabase database, MongoCheckpointStoreOptions options) {
        Checkpoints = Ensure.NotNull(database).GetCollection<Checkpoint>(options.CollectionName);
        _batchSize  = options.BatchSize;
    }

    public MongoCheckpointStore(IMongoDatabase database) : this(database, new MongoCheckpointStoreOptions()) { }

    public MongoCheckpointStore(IMongoDatabase database, IOptions<MongoCheckpointStoreOptions> options)
        : this(database, options.Value) { }

    IMongoCollection<Checkpoint> Checkpoints { get; }

    public async ValueTask<Checkpoint> GetLastCheckpoint(
        string            checkpointId,
        CancellationToken cancellationToken = default
    ) {
        var checkpoint = await Checkpoints.AsQueryable()
            .Where(x => x.Id == checkpointId)
            .SingleOrDefaultAsync(cancellationToken)
            .NoContext() ?? new Checkpoint(checkpointId, null);

        Log.CheckpointLoaded(this, checkpoint);

        _counters[checkpointId] = 0;

        return checkpoint;
    }

    readonly ConcurrentDictionary<string, int> _counters = new();

    public async ValueTask<Checkpoint> StoreCheckpoint(
        Checkpoint        checkpoint,
        CancellationToken cancellationToken = default
    ) {
        _counters[checkpoint.Id]++;
        if (_counters[checkpoint.Id] < _batchSize) return checkpoint;

        await Checkpoints.ReplaceOneAsync(
            x => x.Id == checkpoint.Id,
            checkpoint,
            MongoDefaults.DefaultReplaceOptions,
            cancellationToken
        ).NoContext();

        _counters[checkpoint.Id] = 0;

        Log.CheckpointStored(this, checkpoint);

        return checkpoint;
    }
}

[PublicAPI]
public record MongoCheckpointStoreOptions {
    public string CollectionName { get; init; } = "checkpoint";
    public int    BatchSize      { get; init; }
}