// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Logging;
using Eventuous.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace Eventuous.Postgresql.Subscriptions;

public class PostgresCheckpointStoreOptions {
    // ReSharper disable once ConvertToPrimaryConstructor
    public PostgresCheckpointStoreOptions(string schema = Postgresql.Schema.DefaultSchema)
        => Schema = schema;

    /// <summary>
    /// Override the default schema name.
    /// The property is mutable to allow using ASP.NET Core configuration.
    /// </summary>
    public string Schema { get; set; }
}

public class PostgresCheckpointStore : ICheckpointStore {
    readonly NpgsqlDataSource _dataSource;
    readonly ILoggerFactory?  _loggerFactory;
    readonly string           _getCheckpointSql;
    readonly string           _addCheckpointSql;
    readonly string           _storeCheckpointSql;

    public PostgresCheckpointStore(NpgsqlDataSource dataSource, string schema, ILoggerFactory? loggerFactory) {
        _dataSource    = dataSource;
        _loggerFactory = loggerFactory;
        var sch = new Schema(schema);
        _getCheckpointSql   = sch.GetCheckpointSql;
        _addCheckpointSql   = sch.AddCheckpointSql;
        _storeCheckpointSql = sch.UpdateCheckpointSql;
    }

    [PublicAPI]
    public PostgresCheckpointStore(
        NpgsqlDataSource                          dataSource,
        IOptions<PostgresCheckpointStoreOptions>? options,
        ILoggerFactory?                           loggerFactory
    ) : this(dataSource, options?.Value?.Schema ?? Schema.DefaultSchema, loggerFactory) { }

    public async ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken) {
        Logger.ConfigureIfNull(checkpointId, _loggerFactory);
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).NoContext();
        Checkpoint checkpoint;

        await using (var cmd = GetCheckpointCommand(connection, _getCheckpointSql, checkpointId)) {
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();

            if (await reader.ReadAsync(cancellationToken).NoContext()) {
                var hasPosition = !reader.IsDBNull(0);

                checkpoint = hasPosition
                    ? new Checkpoint(checkpointId, (ulong?) reader.GetInt64(0))
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

    public async ValueTask<Checkpoint> StoreCheckpoint(
        Checkpoint        checkpoint,
        bool              force,
        CancellationToken cancellationToken
    ) {
        if (checkpoint.Position == null) return checkpoint;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).NoContext();
        await using var cmd = GetCheckpointCommand(connection, _storeCheckpointSql, checkpoint.Id);
        cmd.Parameters.AddWithValue("position", NpgsqlDbType.Bigint, (long) checkpoint.Position);
        await cmd.ExecuteNonQueryAsync(cancellationToken).NoContext();
        Logger.Current.CheckpointStored(this, checkpoint, force);
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