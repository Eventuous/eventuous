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

    public MongoCheckpointStore(IMongoDatabase database, MongoCheckpointOptions? options = null) {
        var usedOptions = options ?? new MongoCheckpointOptions();
        Checkpoints = Ensure.NotNull(database).GetCollection<Checkpoint>(usedOptions.CollectionName);
        _batchSize  = usedOptions.BatchSize;
    }

    public MongoCheckpointStore(IMongoDatabase database, IOptions<MongoCheckpointOptions> options)
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

        Log.CheckpointStored(this, checkpoint);

        return checkpoint;
    }
}

[PublicAPI]
public record MongoCheckpointOptions {
    public string CollectionName { get; init; } = "checkpoint";
    public int    BatchSize      { get; init; }
}