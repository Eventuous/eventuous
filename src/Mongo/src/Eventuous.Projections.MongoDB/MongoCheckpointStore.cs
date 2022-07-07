using System.Reactive.Linq;
using System.Reactive.Subjects;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Tools;
using Microsoft.Extensions.Options;
using MongoDB.Driver.Linq;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;
using MongoDefaults = Eventuous.Projections.MongoDB.Tools.MongoDefaults;

namespace Eventuous.Projections.MongoDB;

public class MongoCheckpointStore : ICheckpointStore {
    MongoCheckpointStore(IMongoDatabase database, MongoCheckpointStoreOptions options) {
        Checkpoints = Ensure.NotNull(database).GetCollection<Checkpoint>(options.CollectionName);
        _getSubject = GetSubject;

        Subject<Checkpoint> GetSubject() {
            var subject = new Subject<Checkpoint>();

            var observable = options switch {
                { BatchSize: > 0, BatchIntervalSec: > 0 } => subject.Buffer(
                    TimeSpan.FromSeconds(options.BatchIntervalSec),
                    options.BatchSize
                ),
                { BatchSize: > 0, BatchIntervalSec: 0 } => subject.Buffer(options.BatchSize),
                { BatchSize: 0, BatchIntervalSec: > 0 } => subject.Buffer(
                    TimeSpan.FromSeconds(options.BatchIntervalSec)
                ),
                _ => subject.Select(x => new List<Checkpoint> { x })
            };

            observable
                .Where(x => x.Count > 0)
                .Select(x => Observable.FromAsync(ct => StoreInternal(x.Last(), ct)))
                .Concat()
                .Subscribe();

            return subject;
        }
    }

    readonly Func<Subject<Checkpoint>> _getSubject;

    [PublicAPI]
    public MongoCheckpointStore(IMongoDatabase database) : this(database, new MongoCheckpointStoreOptions()) { }

    [PublicAPI]
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

        _subjects[checkpointId] = _getSubject();

        return checkpoint;
    }

    readonly Dictionary<string, Subject<Checkpoint>> _subjects = new();

    public async ValueTask<Checkpoint> StoreCheckpoint(
        Checkpoint        checkpoint,
        bool              force,
        CancellationToken cancellationToken = default
    ) {
        if (force) {
            await StoreInternal(checkpoint, cancellationToken).NoContext();
            return checkpoint;
        }

        _subjects[checkpoint.Id].OnNext(checkpoint);

        return checkpoint;
    }

    async Task StoreInternal(Checkpoint checkpoint, CancellationToken cancellationToken) {
        await Checkpoints.ReplaceOneAsync(
                x => x.Id == checkpoint.Id,
                checkpoint,
                MongoDefaults.DefaultReplaceOptions,
                cancellationToken
            )
            .NoContext();

        Log.CheckpointStored(this, checkpoint);
    }
}

[PublicAPI]
public record MongoCheckpointStoreOptions {
    public string CollectionName   { get; init; } = "checkpoint";
    public int    BatchSize        { get; init; } = 1;
    public int    BatchIntervalSec { get; init; } = 5;
}
