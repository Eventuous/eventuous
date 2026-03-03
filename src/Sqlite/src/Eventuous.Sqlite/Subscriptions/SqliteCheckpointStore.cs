// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Logging;
using Microsoft.Extensions.Logging;

namespace Eventuous.Sqlite.Subscriptions;

using Extensions;

/// <summary>
/// Checkpoint store for SQLite, which stores checkpoints in a table.
/// Use it when you create read models in SQLite too.
/// </summary>
public class SqliteCheckpointStore : ICheckpointStore {
    readonly ILoggerFactory? _loggerFactory;
    readonly string          _getCheckpointSql;
    readonly string          _addCheckpointSql;
    readonly string          _storeCheckpointSql;
    readonly string          _connectionString;

    public SqliteCheckpointStore(string connectionString, string schemaName, ILoggerFactory? loggerFactory = null) {
        _loggerFactory    = loggerFactory;
        _connectionString = Ensure.NotEmptyString(connectionString);
        var schema = new Schema(schemaName);
        _getCheckpointSql   = schema.GetCheckpointSql;
        _addCheckpointSql   = schema.AddCheckpointSql;
        _storeCheckpointSql = schema.UpdateCheckpointSql;
    }

    public SqliteCheckpointStore(SqliteCheckpointStoreOptions options, ILoggerFactory? loggerFactory)
        : this(options.ConnectionString!, options is { Schema: not null } ? options.Schema : Schema.DefaultSchema, loggerFactory) { }

    /// <inheritdoc />
    public async ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken) {
        Logger.ConfigureIfNull(checkpointId, _loggerFactory);
        await using var connection = await ConnectionFactory.GetConnection(_connectionString, cancellationToken).NoContext();

        Checkpoint checkpoint;

        await using (var cmd = GetCheckpointCommand(connection, _getCheckpointSql, checkpointId)) {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();

            if (await reader.ReadAsync(cancellationToken).NoContext()) {
                var hasPosition = !reader.IsDBNull(0);
                checkpoint = hasPosition ? new(checkpointId, (ulong?)reader.GetInt64(0)) : Checkpoint.Empty(checkpointId);
                Logger.Current.CheckpointLoaded(this, checkpoint);

                return checkpoint;
            }
        }

        await using var add = GetCheckpointCommand(connection, _addCheckpointSql, checkpointId);
        await add.ExecuteNonQueryAsync(cancellationToken).NoContext();
        checkpoint = Checkpoint.Empty(checkpointId);
        Logger.Current.CheckpointLoaded(this, checkpoint);

        return checkpoint;
    }

    /// <inheritdoc />
    public async ValueTask<Checkpoint> StoreCheckpoint(Checkpoint checkpoint, bool force, CancellationToken cancellationToken) {
        if (checkpoint.Position == null) return checkpoint;

        await using var connection = await ConnectionFactory.GetConnection(_connectionString, cancellationToken).NoContext();

        await using var cmd = GetCheckpointCommand(connection, _storeCheckpointSql, checkpoint.Id)
            .Add("@position", (long)checkpoint.Position);

        await cmd.ExecuteNonQueryAsync(cancellationToken).NoContext();
        Logger.Current.CheckpointStored(this, checkpoint, force);

        return checkpoint;
    }

    static SqliteCommand GetCheckpointCommand(SqliteConnection connection, string sql, string checkpointId)
        => connection.GetTextCommand(sql).Add("@checkpointId", checkpointId);
}

/// <summary>
/// SQLite checkpoint store options.
/// </summary>
[PublicAPI]
public record SqliteCheckpointStoreOptions {
    /// <summary>
    /// Name of schema to use
    /// </summary>
    public string? Schema { get; init; }

    /// <summary>
    /// SQLite connection string
    /// </summary>
    public string? ConnectionString { get; init; }
}
