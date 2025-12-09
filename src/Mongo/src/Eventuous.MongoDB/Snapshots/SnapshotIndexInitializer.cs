// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.MongoDB.Snapshots;

/// <summary>
/// Initializes MongoDB indexes for snapshot collection on startup.
/// </summary>
public class SnapshotIndexInitializer : IHostedService {
    readonly IMongoDatabase            _database;
    readonly MongoSnapshotStoreOptions _options;
    readonly ILogger<SnapshotIndexInitializer>? _logger;

    public SnapshotIndexInitializer(
            IMongoDatabase              database,
            MongoSnapshotStoreOptions   options,
            ILoggerFactory?             loggerFactory = null
        ) {
        _database = Ensure.NotNull(database);
        _options  = Ensure.NotNull(options);
        _logger   = loggerFactory?.CreateLogger<SnapshotIndexInitializer>();
    }

    public async Task StartAsync(CancellationToken cancellationToken) {
        if (!_options.InitializeIndexes) return;

        _logger?.LogInformation("Initializing snapshot indexes for collection {CollectionName}", _options.CollectionName);

        var collection = _database.GetCollection<SnapshotDocument>(_options.CollectionName);

        try {
            // Note: StreamName is marked as [BsonId], so it's automatically indexed as _id
            // No need to create a separate index on StreamName
            
            // Create index on Created for potential queries/filtering
            var createdIndex = new CreateIndexModel<SnapshotDocument>(
                Builders<SnapshotDocument>.IndexKeys.Ascending(x => x.Created),
                new CreateIndexOptions { Name = "IX_Snapshots_Created" }
            );

            await collection.Indexes.CreateOneAsync(createdIndex, cancellationToken: cancellationToken).NoContext();

            _logger?.LogInformation("Snapshot indexes initialized successfully");
        } catch (MongoCommandException ex) when (ex.CodeName == "IndexOptionsConflict" || ex.Message.Contains("already exists")) {
            _logger?.LogWarning("Index already exists, skipping creation");
        } catch (Exception ex) {
            _logger?.LogError(ex, "Failed to initialize snapshot indexes");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

