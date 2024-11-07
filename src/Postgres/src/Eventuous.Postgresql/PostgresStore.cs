// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data.Common;
using Eventuous.Postgresql.Extensions;
using Eventuous.Sql.Base;

// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace Eventuous.Postgresql;

public class PostgresStoreOptions(string schema) {
    public PostgresStoreOptions() : this(Postgresql.Schema.DefaultSchema) { }

    /// <summary>
    /// Override the default schema name.
    /// </summary>
    public string Schema { get; set; } = schema;

    /// <summary>
    /// PostgreSQL connection string.
    /// </summary>
    public string ConnectionString { get; set; } = null!;

    /// <summary>
    /// Set to true to initialize the database schema on startup. Default is false.
    /// </summary>
    public bool InitializeDatabase { get; set; }
}

public class PostgresStore : SqlEventStoreBase<NpgsqlConnection, NpgsqlTransaction> {
    readonly NpgsqlDataSource _dataSource;

    public Schema Schema { get; }

    public PostgresStore(
            NpgsqlDataSource      dataSource,
            PostgresStoreOptions? options,
            IEventSerializer?     serializer     = null,
            IMetadataSerializer?  metaSerializer = null
        ) : base(serializer, metaSerializer) {
        var pgOptions = options ?? new PostgresStoreOptions();
        Schema      = new(pgOptions.Schema);
        _dataSource = Ensure.NotNull(dataSource, "Data Source");
    }

    protected override async ValueTask<NpgsqlConnection> OpenConnection(CancellationToken cancellationToken)
        => await _dataSource.OpenConnectionAsync(cancellationToken).NoContext();

    protected override DbCommand GetReadCommand(NpgsqlConnection connection, StreamName stream, StreamReadPosition start, int count)
        => connection.GetCommand(Schema.ReadStreamForwards)
            .Add("_stream_name", NpgsqlDbType.Varchar, stream.ToString())
            .Add("_from_position", NpgsqlDbType.Integer, start.Value)
            .Add("_count", NpgsqlDbType.Integer, count);

    protected override DbCommand GetReadBackwardsCommand(NpgsqlConnection connection, StreamName stream, StreamReadPosition start, int count)
        => connection.GetCommand(Schema.ReadStreamBackwards)
            .Add("_stream_name", NpgsqlDbType.Varchar, stream.ToString())
            .Add("_from_position", NpgsqlDbType.Integer, start.Value)
            .Add("_count", NpgsqlDbType.Integer, count);

    protected override bool IsStreamNotFound(Exception exception)
        => exception is PostgresException e && e.MessageText.StartsWith("StreamNotFound");

    protected override bool IsConflict(Exception exception)
        => exception is PostgresException e && e.MessageText.StartsWith("WrongExpectedVersion");

    protected override DbCommand GetAppendCommand(
            NpgsqlConnection      connection,
            NpgsqlTransaction     transaction,
            StreamName            stream,
            ExpectedStreamVersion expectedVersion,
            NewPersistedEvent[]   events
        ) => connection.GetCommand(Schema.AppendEvents, transaction)
        .Add("_stream_name", NpgsqlDbType.Varchar, stream.ToString())
        .Add("_expected_version", NpgsqlDbType.Integer, expectedVersion.Value)
        .Add("_created", DateTime.UtcNow)
        .Add("_messages", events);

    protected override DbCommand GetStreamExistsCommand(NpgsqlConnection connection, StreamName stream)
        => connection.GetCommand(Schema.StreamExists).Add("name", NpgsqlDbType.Varchar, stream.ToString());

    protected override DbCommand GetTruncateCommand(
            NpgsqlConnection       connection,
            StreamName             stream,
            ExpectedStreamVersion  expectedVersion,
            StreamTruncatePosition position
        )
        => connection.GetCommand(Schema.TruncateStream)
            .Add("_stream_name", NpgsqlDbType.Varchar, stream.ToString())
            .Add("_expected_version", NpgsqlDbType.Integer, expectedVersion.Value)
            .Add("_position", NpgsqlDbType.Integer, position.Value);
}
