// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data.Common;
using System.Text;
using Eventuous.Diagnostics;
using Eventuous.Sql.Base;
using Eventuous.Sqlite.Extensions;

namespace Eventuous.Sqlite;

public record SqliteStoreOptions {
    public string ConnectionString   { get; init; } = "Data Source=eventuous.db";
    public string Schema             { get; init; } = Sqlite.Schema.DefaultSchema;
    public bool   InitializeDatabase { get; init; }
}

public class SqliteStore : SqlEventStoreBase<SqliteConnection, SqliteTransaction> {
    readonly GetSqliteConnection _getConnection;

    public Schema Schema { get; }

    public SqliteStore(SqliteStoreOptions options, IEventSerializer? serializer = null, IMetadataSerializer? metaSerializer = null)
        : base(serializer, metaSerializer) {
        var connectionString = Ensure.NotEmptyString(options.ConnectionString);
        _getConnection = ct => ConnectionFactory.GetConnection(connectionString, ct);
        Schema         = new(options.Schema);
    }

    protected override async ValueTask<SqliteConnection> OpenConnection(CancellationToken cancellationToken)
        => await _getConnection(cancellationToken).NoContext();

    protected override DbCommand GetReadCommand(SqliteConnection connection, StreamName stream, StreamReadPosition start, int count)
        => connection
            .GetTextCommand(
                $"""
                 SELECT m.message_id, m.message_type, m.stream_position, m.global_position,
                        m.json_data, m.json_metadata, m.created
                 FROM {Schema.MessagesTable} m
                 INNER JOIN {Schema.StreamsTable} s ON m.stream_id = s.stream_id
                 WHERE s.stream_name = @stream_name AND m.stream_position >= @from_position
                 ORDER BY m.stream_position
                 LIMIT @count
                 """
            )
            .Add("@stream_name", stream.ToString())
            .Add("@from_position", start.Value)
            .Add("@count", count);

    protected override DbCommand GetReadBackwardsCommand(SqliteConnection connection, StreamName stream, StreamReadPosition start, int count)
        => connection
            .GetTextCommand(
                $"""
                 SELECT m.message_id, m.message_type, m.stream_position, m.global_position,
                        m.json_data, m.json_metadata, m.created
                 FROM {Schema.MessagesTable} m
                 INNER JOIN {Schema.StreamsTable} s ON m.stream_id = s.stream_id
                 WHERE s.stream_name = @stream_name
                   AND m.stream_position <= MIN(@from_position, s.version)
                 ORDER BY m.stream_position DESC
                 LIMIT @count
                 """
            )
            .Add("@stream_name", stream.ToString())
            .Add("@from_position", start.Value)
            .Add("@count", count);

    protected override DbCommand GetAppendCommand(
            SqliteConnection      connection,
            SqliteTransaction     transaction,
            StreamName            stream,
            ExpectedStreamVersion expectedVersion,
            NewPersistedEvent[]   events
        )
        => throw new NotSupportedException("SQLite does not use GetAppendCommand. AppendEvents is overridden directly.");

    [RequiresDynamicCode("Only works with AOT when using DefaultStaticEventSerializer")]
    [RequiresUnreferencedCode("Only works with AOT when using DefaultStaticEventSerializer")]
    public override async Task<AppendEventsResult> AppendEvents(
            StreamName                          stream,
            ExpectedStreamVersion               expectedVersion,
            IReadOnlyCollection<NewStreamEvent> events,
            CancellationToken                   cancellationToken
        ) {
        var persistedEvents = events.Where(x => x.Payload != null).Select(Convert).ToArray();

        if (persistedEvents.Length == 0) return AppendEventsResult.NoOp;

        await using var connection  = await OpenConnection(cancellationToken).NoContext();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).NoContext();

        try {
            // Ensure stream exists (idempotent insert)
            await using (var insertStreamCmd = connection.GetTextCommand(
                $"INSERT OR IGNORE INTO {Schema.StreamsTable} (stream_name, version) VALUES (@name, -1)", transaction
            )) {
                insertStreamCmd.Parameters.AddWithValue("@name", stream.ToString());
                await insertStreamCmd.ExecuteNonQueryAsync(cancellationToken).NoContext();
            }

            // Get current stream state
            int streamId;
            int currentVersion;

            await using (var selectCmd = connection.GetTextCommand(
                $"SELECT stream_id, version FROM {Schema.StreamsTable} WHERE stream_name = @name", transaction
            )) {
                selectCmd.Parameters.AddWithValue("@name", stream.ToString());
                await using var reader = await selectCmd.ExecuteReaderAsync(cancellationToken).NoContext();
                await reader.ReadAsync(cancellationToken).NoContext();
                streamId       = reader.GetInt32(0);
                currentVersion = reader.GetInt32(1);
            }

            // Validate expected version
            if (expectedVersion != ExpectedStreamVersion.Any && currentVersion != expectedVersion.Value) {
                throw new AppendToStreamException(
                    stream,
                    new InvalidOperationException(
                        $"WrongExpectedVersion {expectedVersion.Value}, current version {currentVersion}"
                    )
                );
            }

            // Insert events
            var  now                 = DateTime.UtcNow.ToString("o");
            long lastGlobalPosition = 0;

            for (var i = 0; i < persistedEvents.Length; i++) {
                var evt            = persistedEvents[i];
                var streamPosition = currentVersion + i + 1;

                await using var insertCmd = connection.GetTextCommand(
                    $"""
                     INSERT INTO {Schema.MessagesTable}
                         (message_id, message_type, stream_id, stream_position, json_data, json_metadata, created)
                     VALUES (@message_id, @message_type, @stream_id, @stream_position, @json_data, @json_metadata, @created)
                     RETURNING global_position
                     """,
                    transaction
                );

                insertCmd.Parameters.AddWithValue("@message_id", evt.MessageId.ToString());
                insertCmd.Parameters.AddWithValue("@message_type", evt.MessageType);
                insertCmd.Parameters.AddWithValue("@stream_id", streamId);
                insertCmd.Parameters.AddWithValue("@stream_position", streamPosition);
                insertCmd.Parameters.AddWithValue("@json_data", evt.JsonData);
                insertCmd.Parameters.AddWithValue("@json_metadata", (object?)evt.JsonMetadata ?? DBNull.Value);
                insertCmd.Parameters.AddWithValue("@created", now);

                var result = await insertCmd.ExecuteScalarAsync(cancellationToken).NoContext();
                lastGlobalPosition = System.Convert.ToInt64(result);
            }

            // Update stream version
            var newVersion = currentVersion + persistedEvents.Length;

            await using (var updateCmd = connection.GetTextCommand(
                $"UPDATE {Schema.StreamsTable} SET version = @version WHERE stream_id = @id", transaction
            )) {
                updateCmd.Parameters.AddWithValue("@version", newVersion);
                updateCmd.Parameters.AddWithValue("@id", streamId);
                await updateCmd.ExecuteNonQueryAsync(cancellationToken).NoContext();
            }

            await transaction.CommitAsync(cancellationToken).NoContext();

            return new AppendEventsResult((ulong)lastGlobalPosition, newVersion);
        } catch (AppendToStreamException) {
            await transaction.RollbackAsync(cancellationToken).NoContext();

            throw;
        } catch (Exception e) {
            await transaction.RollbackAsync(cancellationToken).NoContext();
            PersistenceEventSource.Log.UnableToAppendEvents(stream, e);

            throw IsConflict(e) ? new AppendToStreamException(stream, e) : e;
        }

        [RequiresUnreferencedCode("Calls Eventuous.IEventSerializer.SerializeEvent(Object)")]
        [RequiresDynamicCode("Calls Eventuous.IEventSerializer.SerializeEvent(Object)")]
        NewPersistedEvent Convert(NewStreamEvent evt) {
            var data = Serializer.SerializeEvent(evt.Payload!);
            var meta = MetaSerializer.Serialize(evt.Metadata);

            return new(evt.Id, data.EventType, AsString(data.Payload), AsString(meta));
        }

        string AsString(ReadOnlySpan<byte> bytes) => Encoding.UTF8.GetString(bytes);
    }

    protected override bool IsStreamNotFound(Exception exception) => false;

    protected override bool IsConflict(Exception exception) => exception is SqliteException { SqliteErrorCode: 19 };

    protected override DbCommand GetStreamExistsCommand(SqliteConnection connection, StreamName stream)
        => connection.GetTextCommand(Schema.StreamExists).Add("@name", stream.ToString());

    protected override DbCommand GetTruncateCommand(
            SqliteConnection       connection,
            StreamName             stream,
            ExpectedStreamVersion  expectedVersion,
            StreamTruncatePosition position
        )
        => connection
            .GetTextCommand(
                $"""
                 DELETE FROM {Schema.MessagesTable}
                 WHERE stream_id = (SELECT stream_id FROM {Schema.StreamsTable} WHERE stream_name = @stream_name)
                   AND stream_position < @position
                 """
            )
            .Add("@stream_name", stream.ToString())
            .Add("@position", position.Value);
}
