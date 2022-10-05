// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data;
using Eventuous.SqlServer.Extensions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Logging;
using Microsoft.Data.SqlClient;

namespace Eventuous.SqlServer.Subscriptions;

public class SqlServerCheckpointStore : ICheckpointStore {
    readonly GetSqlServerConnection _getConnection;
    readonly string                 _getCheckpointSql;
    readonly string                 _addCheckpointSql;
    readonly string                 _storeCheckpointSql;

    public SqlServerCheckpointStore(GetSqlServerConnection getConnection, string schema) {
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
                Logger.Current.CheckpointLoaded(this, checkpoint);
                return checkpoint;
            }
        }

        await using var add = GetCheckpointCommand(connection, _addCheckpointSql, checkpointId);
        await add.ExecuteNonQueryAsync(cancellationToken).NoContext();
        checkpoint = new Checkpoint(checkpointId, null);
        Logger.Current.CheckpointLoaded(this, checkpoint);
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
        cmd.Parameters.AddWithValue("position", SqlDbType.BigInt, (long)checkpoint.Position);
        await cmd.ExecuteNonQueryAsync(cancellationToken).NoContext();
        Logger.Current.CheckpointStored(this, checkpoint, force);
        return checkpoint;
    }

    static SqlCommand GetCheckpointCommand(SqlConnection connection, string sql, string checkpointId) {
        var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("checkpointId", SqlDbType.NVarChar, checkpointId);
        return cmd;
    }
}
