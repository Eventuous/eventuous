// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Logging;

namespace Eventuous.SqlServer.Subscriptions;

using Extensions;

/// <summary>
/// Checkpoint store for SQL Server, which stores checkpoints in a table.
/// Use it when you create read models in SQL Server too.
/// </summary>
public class SqlServerCheckpointStore : ICheckpointStore {
    readonly string _getCheckpointSql;
    readonly string _addCheckpointSql;
    readonly string _storeCheckpointSql;
    readonly string _connectionString;

    public SqlServerCheckpointStore(SqlServerCheckpointStoreOptions options) {
        _connectionString = Ensure.NotEmptyString(options.ConnectionString);
        var schema = options is { Schema: not null } ? new Schema(options.Schema) : new Schema();
        _getCheckpointSql   = schema.GetCheckpointSql;
        _addCheckpointSql   = schema.AddCheckpointSql;
        _storeCheckpointSql = schema.UpdateCheckpointSql;
    }

    public async ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken) {
        await using var connection = await ConnectionFactory.GetConnection(_connectionString, cancellationToken).NoContext();
        Checkpoint checkpoint;

        await using (var cmd = GetCheckpointCommand(connection, _getCheckpointSql, checkpointId)) {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();

            if (await reader.ReadAsync(cancellationToken).NoContext()) {
                var hasPosition = !reader.IsDBNull(0);

                checkpoint = hasPosition
                    ? new Checkpoint(checkpointId, (ulong?)reader.GetInt64(0))
                    : Checkpoint.Empty(checkpointId);

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

    public async ValueTask<Checkpoint> StoreCheckpoint(Checkpoint checkpoint, bool force, CancellationToken cancellationToken) {
        if (checkpoint.Position == null) return checkpoint;

        await using var connection = await ConnectionFactory.GetConnection(_connectionString, cancellationToken).NoContext();

        await using var cmd = GetCheckpointCommand(connection, _storeCheckpointSql, checkpoint.Id)
            .Add("position", SqlDbType.BigInt, (long)checkpoint.Position);

        await cmd.ExecuteNonQueryAsync(cancellationToken).NoContext();
        Logger.Current.CheckpointStored(this, checkpoint, force);

        return checkpoint;
    }

    static SqlCommand GetCheckpointCommand(SqlConnection connection, string sql, string checkpointId)
        => connection.GetTextCommand(sql).Add("checkpointId", SqlDbType.NVarChar, checkpointId);
}

/// <summary>
/// SQL Server checkpoint store options.
/// </summary>
[PublicAPI]
public record SqlServerCheckpointStoreOptions {
    /// <summary>
    /// Name of schema to use
    /// </summary>
    public string? Schema { get; init; }

    /// <summary>
    /// SQL Server connection string
    /// </summary>
    public string? ConnectionString { get; init; }
}
