// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Postgresql;
using Eventuous.Postgresql.Extensions;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Sql.Base;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.Postgres.Subscriptions;

public class TombstonesFixture(
        Action<PostgresAllStreamSubscriptionOptions> configureOptions
    )
    : SubscriptionFixture<PostgresStore, PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, TestEventHandler>(configureOptions, false) {
    protected internal new TestEventHandler Handler => base.Handler;
    protected internal new ValueTask StartSubscription() => base.StartSubscription();
    protected internal new ValueTask StopSubscription() => base.StopSubscription();

    /// <summary>
    /// Two events, then a global position burnt by a rolled back append, then three more events. The burnt
    /// position is never filled, so only gap handling decides whether the last three are ever seen.
    /// </summary>
    public async Task ArrangePermanentGap(StreamName streamName) {
        await this.AppendEvents(streamName, [.. this.CreateEvents(2)], ExpectedStreamVersion.NoStream);
        await InsertGap(streamName, 1);
        await this.AppendEvents(streamName, [.. this.CreateEvents(3)], ExpectedStreamVersion.Any);
    }

    /// <summary>
    /// Polls the handled messages until <paramref name="count"/> arrive or the timeout expires, and returns
    /// how many actually arrived so the caller can assert on it.
    /// </summary>
    public async Task<int> WaitForHandled(int count, TimeSpan timeout, CancellationToken cancellationToken) {
        var deadline = DateTime.UtcNow + timeout;

        while (Handler.Handled.Count < count && DateTime.UtcNow < deadline) {
            await Task.Delay(50, cancellationToken);
        }

        return Handler.Handled.Count;
    }

    public async Task InsertGap(StreamName streamName, int expectedVersion) {
        await using var conn = await DataSource.OpenConnectionAsync();
        await using var tx   = await conn.BeginTransactionAsync();

        var appendCmd = conn.GetCommand($"select * from {SchemaName}.append_events(@_stream_name, @_expected_version, @_created, @_messages, @_enable_delay)", tx)
            .Add("_stream_name", NpgsqlTypes.NpgsqlDbType.Varchar, streamName.ToString())
            .Add("_expected_version", NpgsqlTypes.NpgsqlDbType.Integer, expectedVersion)
            .Add("_created", DateTime.UtcNow)
            .Add("_messages", new[] { new NewPersistedEvent(Guid.NewGuid(), "GapTemp", "{}", null) })
            .Add("_enable_delay", false);
        await appendCmd.ExecuteNonQueryAsync();
        await tx.RollbackAsync(); // leaves a gap in global_position sequence
    }

    public async Task<long> CountTombstones() {
        await using var conn = await DataSource.OpenConnectionAsync();

        var cmd = conn.GetCommand($"select count(1) from {SchemaName}.messages where message_type = @_message_type")
            .Add("_message_type", PostgresSubscriptionConstants.TombstoneMessageType);
        var result = await cmd.ExecuteScalarAsync();

        return result is DBNull ? 0 : (long)result!;
    }
}
