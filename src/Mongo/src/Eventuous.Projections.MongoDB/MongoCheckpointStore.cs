// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reactive.Linq;
using System.Reactive.Subjects;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver.Linq;
using EventuousCheckpoint = Eventuous.Subscriptions.Checkpoints.Checkpoint;

namespace Eventuous.Projections.MongoDB;

using MongoDefaults = Eventuous.Projections.MongoDB.Tools.MongoDefaults;

/// <summary>
/// Checkpoint store for MongoDB, which stores checkpoints in a collection.
/// Use it when you create read models in MongoDB too.
/// </summary>
public class MongoCheckpointStore(IMongoDatabase database, MongoCheckpointStoreOptions options, ILoggerFactory loggerFactory) : ICheckpointStore {
    [PublicAPI]
    public MongoCheckpointStore(IMongoDatabase database, ILoggerFactory loggerFactory)
        : this(database, new MongoCheckpointStoreOptions(), loggerFactory) { }

    [PublicAPI]
    public MongoCheckpointStore(IMongoDatabase database, IOptions<MongoCheckpointStoreOptions> options, ILoggerFactory loggerFactory)
        : this(database, options.Value, loggerFactory) { }

    IMongoCollection<Checkpoint> Checkpoints { get; } = Ensure.NotNull(database).GetCollection<Checkpoint>(options.CollectionName);

    public async ValueTask<EventuousCheckpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken = default) {
        var storedCheckpoint = await Checkpoints.AsQueryable()
            .Where(x => x.Id == checkpointId)
            .SingleOrDefaultAsync(cancellationToken)
            .NoContext();

        var checkpoint = storedCheckpoint?.ToCheckpoint() ?? EventuousCheckpoint.Empty(checkpointId);

        Logger.Current.CheckpointLoaded(this, checkpoint);

        _subjects[checkpointId] = GetSubject();

        return checkpoint;
    }

    readonly Dictionary<string, Subject<EventuousCheckpoint>> _subjects = new();

    Subject<EventuousCheckpoint> GetSubject() {
        var subject = new Subject<EventuousCheckpoint>();

        var observable = options switch {
            { BatchSize: > 0, BatchIntervalSec: > 0 } => subject.Buffer(TimeSpan.FromSeconds(options.BatchIntervalSec), options.BatchSize),
            { BatchSize: > 0, BatchIntervalSec: 0 }   => subject.Buffer(options.BatchSize),
            { BatchSize: 0, BatchIntervalSec: > 0 }   => subject.Buffer(TimeSpan.FromSeconds(options.BatchIntervalSec)),
            _                                         => subject.Select(x => new List<EventuousCheckpoint> { x })
        };

        observable
            .Where(x => x.Count > 0)
            .Select(x => Observable.FromAsync(ct => StoreInternal(x.Last(), false, ct)))
            .Concat()
            .Subscribe();

        return subject;
    }

    public async ValueTask<EventuousCheckpoint> StoreCheckpoint(EventuousCheckpoint checkpoint, bool force, CancellationToken cancellationToken = default) {
        if (force) {
            await StoreInternal(checkpoint, true, cancellationToken).NoContext();

            return checkpoint;
        }

        _subjects[checkpoint.Id].OnNext(checkpoint);

        return checkpoint;
    }

    async Task StoreInternal(EventuousCheckpoint checkpoint, bool force, CancellationToken cancellationToken) {
        await Checkpoints.ReplaceOneAsync(
                x => x.Id == checkpoint.Id,
                Checkpoint.FromCheckpoint(checkpoint),
                MongoDefaults.DefaultReplaceOptions,
                cancellationToken
            )
            .NoContext();

        Logger.ConfigureIfNull(checkpoint.Id, loggerFactory);
        Logger.Current.CheckpointStored(this, checkpoint, force);
    }

    record Checkpoint(string Id, ulong? Position) {
        public static Checkpoint FromCheckpoint(EventuousCheckpoint checkpoint) => new(checkpoint.Id, checkpoint.Position);

        public EventuousCheckpoint ToCheckpoint() => new(Id, Position);
    }
}

/// <summary>
/// MongoDB checkpoint store options.
/// </summary>
[PublicAPI]
public record MongoCheckpointStoreOptions {
    /// <summary>
    /// Collection for checkpoint documents (one per subscription). Default is "checkpoint".
    /// </summary>
    public string CollectionName { get; init; } = "checkpoint";

    /// <summary>
    /// Commit batch size, default is 1. Increase it to improve performance.
    /// </summary>
    public int BatchSize { get; init; } = 1;

    /// <summary>
    /// Commit batch interval in seconds, default is 5. Increase it to improve performance.
    /// </summary>
    public int BatchIntervalSec { get; init; } = 5;
}
