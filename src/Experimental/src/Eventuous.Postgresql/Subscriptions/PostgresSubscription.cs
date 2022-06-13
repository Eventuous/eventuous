// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data;
using System.Runtime.Serialization;
using System.Text;
using Eventuous.Postgresql.Extensions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Npgsql;
using NpgsqlTypes;

namespace Eventuous.Postgresql.Subscriptions;

public class PostgresSubscription : EventSubscriptionWithCheckpoint<PostgresSubscriptionOptions> {
    readonly Schema                _schema;
    readonly GetPostgresConnection _getConnection;
    readonly IEventSerializer      _serializer;
    readonly IMetadataSerializer   _metaSerializer;

    public PostgresSubscription(
        GetPostgresConnection       getConnection,
        PostgresSubscriptionOptions options,
        ICheckpointStore            checkpointStore,
        ConsumePipe                 consumePipe
    ) : base(options, checkpointStore, consumePipe, options.ConcurrencyLimit) {
        _schema         = new Schema(options.Schema);
        _getConnection  = Ensure.NotNull(getConnection, "Connection factory");
        _serializer     = options.EventSerializer ?? DefaultEventSerializer.Instance;
        _metaSerializer = DefaultMetadataSerializer.Instance;
    }

    async Task<NpgsqlConnection> OpenConnection(CancellationToken cancellationToken) {
        var connection = _getConnection();
        await connection.OpenAsync(cancellationToken).NoContext();
        return connection;
    }

    protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
        var (_, position) = await GetCheckpoint(cancellationToken).NoContext();

        _cts    = new CancellationTokenSource();
        _runner = Task.Run(() => PollingQuery(position, _cts.Token), _cts.Token);
    }

    protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
        try {
            _cts.Cancel();
            await _runner.NoContext();
        }
        catch (OperationCanceledException) {
            // Nothing to do
        }
    }

    async Task PollingQuery(ulong? position, CancellationToken cancellationToken) {
        var start = position.HasValue ? (long)position : -1;

        while (!cancellationToken.IsCancellationRequested) {
            try {
                await using var connection = await OpenConnection(cancellationToken).NoContext();
                await using var cmd        = new NpgsqlCommand(_schema.ReadAllForwards, connection);

                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("_from_position", NpgsqlDbType.Integer, start);
                cmd.Parameters.AddWithValue("_count", NpgsqlDbType.Integer, Options.MaxPageSize);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();

                var result = reader.ReadEvents(cancellationToken);

                await foreach (var persistedEvent in result.WithCancellation(cancellationToken).ConfigureAwait(false)) {
                    await HandleInternal(ToConsumeContext(persistedEvent, cancellationToken)).NoContext();
                }
            }
            catch (OperationCanceledException) {
                // Nothing to do
            }
            catch (PostgresException e) when (e.IsTransient) {
                // Try again
            }
            catch (Exception e) {
                IsDropped = true;
                Console.WriteLine(e);
                throw;
            }
        }
    }

    const string ContentType = "application/json";

    ulong                   _sequence;
    CancellationTokenSource _cts;
    Task                    _runner;

    IMessageConsumeContext ToConsumeContext(PersistedEvent evt, CancellationToken cancellationToken) {
        var deserialized = _serializer.DeserializeEvent(
            Encoding.UTF8.GetBytes(evt.JsonData),
            evt.MessageType,
            ContentType
        );

        var meta = evt.JsonMetadata == null
            ? new Metadata()
            : _metaSerializer.Deserialize(Encoding.UTF8.GetBytes(evt.JsonMetadata!));

        return deserialized switch {
            SuccessfullyDeserialized success => AsContext(success.Payload),
            FailedToDeserialize failed => throw new SerializationException(
                $"Can't deserialize {evt.MessageType}: {failed.Error}"
            ),
            _ => throw new Exception("Unknown deserialization result")
        };

        IMessageConsumeContext AsContext(object e)
            => new MessageConsumeContext(
                evt.MessageId.ToString(),
                evt.MessageType,
                ContentType,
                Ensure.NotEmptyString(evt.StreamName),
                (ulong)evt.StreamPosition,
                (ulong)evt.GlobalPosition,
                _sequence++,
                evt.Created,
                e,
                meta,
                Options.SubscriptionId,
                cancellationToken
            );
    }
}

public record PostgresSubscriptionOptions : SubscriptionOptions {
    public string Schema           { get; set; } = null!;
    public int    ConcurrencyLimit { get; set; } = 1;
    public int    MaxPageSize      { get; set; } = 1024;
}
