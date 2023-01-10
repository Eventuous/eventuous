// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reactive.Linq;
using System.Reactive.Subjects;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Logging;
using Eventuous.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver.Linq;
using MongoDefaults = Eventuous.Projections.MongoDB.Tools.MongoDefaults;
using EventuousCheckpoint = Eventuous.Subscriptions.Checkpoints.Checkpoint;

namespace Eventuous.Projections.MongoDB;

public class MongoCheckpointStore : ICheckpointStore {
    MongoCheckpointStore(IMongoDatabase database, MongoCheckpointStoreOptions options, ILoggerFactory loggerFactory) {
        _loggerFactory = loggerFactory;
        Checkpoints    = Ensure.NotNull(database).GetCollection<Checkpoint>(options.CollectionName);
        _getSubject    = GetSubject;

        Subject<EventuousCheckpoint> GetSubject() {
            var subject = new Subject<EventuousCheckpoint>();

            var observable = options switch {
                { BatchSize: > 0, BatchIntervalSec: > 0 } => subject.Buffer(
                    TimeSpan.FromSeconds(options.BatchIntervalSec),
                    options.BatchSize
                ),
                { BatchSize: > 0, BatchIntervalSec: 0 } => subject.Buffer(options.BatchSize),
                { BatchSize: 0, BatchIntervalSec: > 0 } => subject.Buffer(
                    TimeSpan.FromSeconds(options.BatchIntervalSec)
                ),
                _ => subject.Select(x => new List<EventuousCheckpoint> { x })
            };

            observable
                .Where(x => x.Count > 0)
                .Select(x => Observable.FromAsync(ct => StoreInternal(x.Last(), false, ct)))
                .Concat()
                .Subscribe();

            return subject;
        }
    }

    readonly Func<Subject<EventuousCheckpoint>> _getSubject;
    readonly ILoggerFactory                     _loggerFactory;

    [PublicAPI]
    public MongoCheckpointStore(IMongoDatabase database, ILoggerFactory loggerFactory)
        : this(database, new MongoCheckpointStoreOptions(), loggerFactory) { }

    [PublicAPI]
    public MongoCheckpointStore(
        IMongoDatabase                        database,
        IOptions<MongoCheckpointStoreOptions> options,
        ILoggerFactory                        loggerFactory
    )
        : this(database, options.Value, loggerFactory) { }

    IMongoCollection<Checkpoint> Checkpoints { get; }

    public async ValueTask<EventuousCheckpoint> GetLastCheckpoint(
        string            checkpointId,
        CancellationToken cancellationToken = default
    ) {
        var storedCheckpoint = await Checkpoints.AsQueryable()
            .Where(x => x.Id == checkpointId)
            .SingleOrDefaultAsync(cancellationToken)
            .NoContext();

        var checkpoint = storedCheckpoint?.ToCheckpoint() ?? EventuousCheckpoint.Empty(checkpointId);

        Logger.Current.CheckpointLoaded(this, checkpoint);

        _subjects[checkpointId] = _getSubject();

        return checkpoint;
    }

    readonly Dictionary<string, Subject<EventuousCheckpoint>> _subjects = new();

    public async ValueTask<EventuousCheckpoint> StoreCheckpoint(
        EventuousCheckpoint checkpoint,
        bool                force,
        CancellationToken   cancellationToken = default
    ) {
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

        Logger.ConfigureIfNull(checkpoint.Id, _loggerFactory);
        Logger.Current.CheckpointStored(this, checkpoint, force);
    }

    record Checkpoint(string Id, ulong? Position) {
        public static Checkpoint FromCheckpoint(EventuousCheckpoint checkpoint) =>
            new(checkpoint.Id, checkpoint.Position);

        public EventuousCheckpoint ToCheckpoint() => new(Id, Position);
    }
}

[PublicAPI]
public record MongoCheckpointStoreOptions {
    public string CollectionName   { get; init; } = "checkpoint";
    public int    BatchSize        { get; init; } = 1;
    public int    BatchIntervalSec { get; init; } = 5;
}