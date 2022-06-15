// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data;
using Eventuous.Subscriptions.Checkpoints;
using Npgsql;
using NpgsqlTypes;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;

namespace Eventuous.Postgresql.Subscriptions;

public class PostgresCheckpointStore : ICheckpointStore {
    readonly GetPostgresConnection _getConnection;
    readonly string                _getCheckpointSql;
    readonly string                _addCheckpointSql;
    readonly string                _storeCheckpointSql;

    public PostgresCheckpointStore(GetPostgresConnection getConnection, string schema) {
        _getConnection = getConnection;
        var sch = new Schema(schema);
        _getCheckpointSql   = sch.GetCheckpointSql;
        _addCheckpointSql   = sch.AddCheckpointSql;
        _storeCheckpointSql = sch.UpdateCheckpointSql;
    }

    public async ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken) {
        await using var connection = _getConnection();
        await connection.OpenAsync(cancellationToken).NoContext();
        Checkpoint checkpoint;

        await using (var cmd = GetCheckpointCommand(connection, _getCheckpointSql, checkpointId)) {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();

            if (await reader.ReadAsync(cancellationToken).NoContext()) {
                checkpoint = new Checkpoint(checkpointId, (ulong?)reader.GetInt64(0));
                Log.CheckpointLoaded(this, checkpoint);
                return checkpoint;
            }
        }

        await using var add = GetCheckpointCommand(connection, _addCheckpointSql, checkpointId);
        await add.ExecuteNonQueryAsync(cancellationToken).NoContext();
        checkpoint = new Checkpoint(checkpointId, null);
        Log.CheckpointLoaded(this, checkpoint);
        return checkpoint;
    }

    public async ValueTask<Checkpoint> StoreCheckpoint(
        Checkpoint        checkpoint,
        bool              force,
        CancellationToken cancellationToken
    ) {
        if (checkpoint.Position == null) return checkpoint;

        await using var connection = _getConnection();
        await connection.OpenAsync(cancellationToken).NoContext();
        await using var cmd = GetCheckpointCommand(connection, _storeCheckpointSql, checkpoint.Id);
        cmd.Parameters.AddWithValue("position", NpgsqlDbType.Bigint, (long)checkpoint.Position);
        await cmd.ExecuteNonQueryAsync(cancellationToken).NoContext();
        Log.CheckpointStored(this, checkpoint);
        return checkpoint;
    }

    static NpgsqlCommand GetCheckpointCommand(NpgsqlConnection connection, string sql, string checkpointId) {
        var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("checkpointId", NpgsqlDbType.Varchar, checkpointId);
        return cmd;
    }
}
