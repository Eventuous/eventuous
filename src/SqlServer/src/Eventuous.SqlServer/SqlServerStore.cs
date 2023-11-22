// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data.Common;
using Eventuous.Sql.Base;
using Eventuous.SqlServer.Extensions;

// ReSharper disable ConvertClosureToMethodGroup

namespace Eventuous.SqlServer;

public delegate SqlConnection GetSqlServerConnection();

public record SqlServerStoreOptions(string Schema = Schema.DefaultSchema);

public class SqlServerStore(
        GetSqlServerConnection getConnection,
        SqlServerStoreOptions  options,
        IEventSerializer?      serializer     = null,
        IMetadataSerializer?   metaSerializer = null
    )
    : SqlEventStoreBase<SqlConnection, SqlTransaction>(serializer, metaSerializer) {
    readonly GetSqlServerConnection _getConnection = Ensure.NotNull(getConnection, "Connection factory");
    readonly Schema                 _schema        = new(options.Schema);

    protected override async ValueTask<SqlConnection> OpenConnection(CancellationToken cancellationToken) {
        var connection = _getConnection();
        await connection.OpenAsync(cancellationToken).NoContext();

        return connection;
    }

    protected override DbCommand GetReadCommand(SqlConnection connection, StreamName stream, StreamReadPosition start, int count)
        => connection
            .GetStoredProcCommand(_schema.ReadStreamForwards)
            .Add("@stream_name", SqlDbType.NVarChar, stream.ToString())
            .Add("@from_position", SqlDbType.Int, start.Value)
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
