// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data.Common;
using Eventuous.Postgresql;
using Eventuous.Postgresql.Extensions;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Sql.Base;
using Eventuous.Tests.Subscriptions.Base;
using Npgsql;
using NpgsqlTypes;
using Testcontainers.PostgreSql;

namespace Eventuous.Tests.Postgres.Subscriptions;

[NotInParallel]
public class SubscriptionGapsDetection()
    : SubscriptionGapsDetectionBase<PostgreSqlContainer, PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, PostgresCheckpointStore>(
        new SubscriptionFixture<DelayedPostgresStore, PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, TestEventHandler>(ConfigureOptions, false)
    ) {
    [Test]
    public async Task Postgres_ShouldNotSkipEvents(CancellationToken cancellationToken) {
        await ShouldNotSkipEvents(cancellationToken);
    }

    static void ConfigureOptions(PostgresAllStreamSubscriptionOptions options) {
        options.Polling.MinIntervalMs = 0;
        options.Polling.MaxIntervalMs = 0;
    }

    internal class DelayedPostgresStore(NpgsqlDataSource dataSource, PostgresStoreOptions? options, IEventSerializer? serializer = null, IMetadataSerializer? metaSerializer = null)
        : PostgresStore(dataSource, options, serializer, metaSerializer) {
        protected override DbCommand GetAppendCommand(NpgsqlConnection connection, NpgsqlTransaction transaction, StreamName stream, ExpectedStreamVersion expectedVersion, NewPersistedEvent[] events)
            => connection.GetCommand($"select * from {Schema.Name}.append_events(@_stream_name, @_expected_version, @_created, @_messages, @_enable_delay)", transaction)
                .Add("_stream_name", NpgsqlDbType.Varchar, stream.ToString())
                .Add("_expected_version", NpgsqlDbType.Integer, expectedVersion.Value)
                .Add("_created", DateTime.UtcNow)
                .Add("_messages", events)
                .Add("_enable_delay", true);
    }
}
