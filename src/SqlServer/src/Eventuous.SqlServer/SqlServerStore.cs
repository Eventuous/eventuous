// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data.Common;
using Eventuous.Sql.Base;
using Eventuous.SqlServer.Extensions;

// ReSharper disable ConvertClosureToMethodGroup

namespace Eventuous.SqlServer;

public record SqlServerStoreOptions {
    public string? ConnectionString   { get; init; }
    public string  Schema             { get; init; } = SqlServer.Schema.DefaultSchema;
    public bool    InitializeDatabase { get; init; }
}

public class SqlServerStore : SqlEventStoreBase<SqlConnection, SqlTransaction> {
    readonly GetSqlServerConnection _getConnection;
    readonly Schema                 _schema;

    public SqlServerStore(SqlServerStoreOptions options, IEventSerializer? serializer = null, IMetadataSerializer? metaSerializer = null)
        : base(serializer, metaSerializer) {
        var connectionString = Ensure.NotEmptyString(options.ConnectionString);
        _getConnection = ct => ConnectionFactory.GetConnection(connectionString, ct);
        _schema        = new Schema(options.Schema);
    }

    protected override async ValueTask<SqlConnection> OpenConnection(CancellationToken cancellationToken) {
        return await _getConnection(cancellationToken).NoContext();
    }

    protected override DbCommand GetReadCommand(SqlConnection connection, StreamName stream, StreamReadPosition start, int count)
        => connection
            .GetStoredProcCommand(_schema.ReadStreamForwards)
            .Add("@stream_name", SqlDbType.NVarChar, stream.ToString())
            .Add("@from_position", SqlDbType.Int, start.Value)
            .Add("@count", SqlDbType.Int, count);

    protected override DbCommand GetReadBackwardsCommand(SqlConnection connection, StreamName stream, int count)
        => connection
            .GetStoredProcCommand(_schema.ReadStreamForwards)
            .Add("@stream_name", SqlDbType.NVarChar, stream.ToString())
            .Add("@count", SqlDbType.Int, count);

    protected override bool IsStreamNotFound(Exception exception) => exception is SqlException e && e.Message.StartsWith("StreamNotFound");

    protected override DbCommand GetAppendCommand(
            SqlConnection         connection,
            SqlTransaction        transaction,
            StreamName            stream,
            ExpectedStreamVersion expectedVersion,
            NewPersistedEvent[]   events
        )
        => connection.GetStoredProcCommand(_schema.AppendEvents, (SqlTransaction)transaction)
            .Add("@stream_name", SqlDbType.NVarChar, stream.ToString())
            .Add("@expected_version", SqlDbType.Int, expectedVersion.Value)
            .Add("@created", SqlDbType.DateTime2, DateTime.UtcNow)
            .AddPersistedEvent("@messages", events);

    protected override bool IsConflict(Exception exception) => exception is SqlException e && e.Number == 50000;

    protected override DbCommand GetStreamExistsCommand(SqlConnection connection, StreamName stream)
        => connection.GetTextCommand(_schema.StreamExists)
            .Add("@name", SqlDbType.NVarChar, stream.ToString());
}
