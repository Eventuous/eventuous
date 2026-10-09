// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

extern alias Sqlite;

using System.Diagnostics.Metrics;
using Eventuous.Sqlite;
using Eventuous.Sqlite.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Registrations;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using SQLitePCL;

// Select the store helper explicitly: Persistence exposes the same internal extensions.
using static Sqlite::Eventuous.Tools.TaskExtensions;

namespace Eventuous.Tests.Sqlite.Metrics;

[NotInParallel]
public class SharedSourceGapMetricsTests {
    [Test]
    public async Task All_stream_subscriptions_read_shared_tail_once_per_collection(CancellationToken cancellationToken) {
        var schema = new Schema($"metrics_{Guid.NewGuid():N}");
        // URI mode=memory keeps this a real in-memory database while allowing pooling.
        // Mode=Memory and Data Source=:memory: disable Microsoft.Data.Sqlite pooling.
        // An isolated pool lets the native profiler observe the actual production connection,
        // without overriding subscriptions or adding a production connection factory hook.
        var connectionString = $"Data Source=file:{schema.SchemaName}?mode=memory&cache=shared;Pooling=True";
        await using var keeper = new SqliteConnection(connectionString + ";Pooling=False");
        await keeper.OpenAsync(cancellationToken).NoContext();
        await using (var command = keeper.CreateCommand()) {
            command.CommandText = "SELECT file FROM pragma_database_list WHERE name = 'main'";
            (await command.ExecuteScalarAsync(cancellationToken).NoContext()).ShouldBe("");
        }
        await using var queries = await TailQueryCounter.Create(connectionString, $"SELECT MAX(global_position) FROM {schema.MessagesTable}", cancellationToken).NoContext();
        await schema.CreateSchema(connectionString, null, cancellationToken).NoContext();
        var mapper = new TypeMapper();
        mapper.AddType<GapEvent>("shared-gap-event");
        var store = new SqliteStore(new() { ConnectionString = connectionString, Schema = schema.SchemaName }, new DefaultEventSerializer(new(), mapper));
        // SQLite global positions start at one: ten events leave the tail at ten.
        await Append(10).NoContext();

        var services = new ServiceCollection();
        foreach (var id in new[] { "first", "second", "caught-up" }) {
            services.AddSubscription<SqliteAllStreamSubscription, SqliteAllStreamSubscriptionOptions>(id, builder => builder
                .Configure(options => {
                    options.ConnectionString = connectionString;
                    options.Schema = schema.SchemaName;
                })
                .UseCheckpointStore<NoOpCheckpointStore>());
        }
        await using var provider = services.BuildServiceProvider();
        var subscriptions = provider.GetServices<SqliteAllStreamSubscription>().ToArray();
        subscriptions.Length.ShouldBe(3);
        subscriptions.Distinct().Count().ShouldBe(3);
        using var metrics = new SubscriptionMetrics(provider.GetServices<GetSubscriptionEndOfStream>());
        // Keep polling stopped; commit deterministic positions through the production handler.
        await Commit("first", 3).NoContext();
        await Commit("second", 7).NoContext();
        await Commit("caught-up", 10).NoContext();

        Dictionary<string, long> gaps = new();
        using var listener = new MeterListener {
            InstrumentPublished = (instrument, owner) => {
                if (instrument.Meter.Name == SubscriptionMetrics.MeterName && instrument.Name == SubscriptionMetrics.GapCountMetricName)
                    owner.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
            gaps.Add((string)tags.ToArray().Single(x => x.Key == SubscriptionMetrics.SubscriptionIdTag).Value!, value));
        listener.Start();

        queries.Count.ShouldBe(0);
        listener.RecordObservableInstruments();
        gaps.Count.ShouldBe(3);
        gaps["first"].ShouldBe(7);
        gaps["second"].ShouldBe(3);
        gaps["caught-up"].ShouldBe(0);
        queries.Count.ShouldBe(1);

        await Append(10).NoContext();
        await Commit("second", 12).NoContext();
        queries.Count.ShouldBe(1);
        gaps.Clear();
        listener.RecordObservableInstruments();
        gaps.Count.ShouldBe(3);
        gaps["first"].ShouldBe(17);
        gaps["second"].ShouldBe(8);
        gaps["caught-up"].ShouldBe(10);
        queries.Count.ShouldBe(2);

        Task<AppendEventsResult> Append(int count) => store.AppendEvents(
            new StreamName("shared-source"), ExpectedStreamVersion.Any,
            Enumerable.Range(0, count).Select(x => new NewStreamEvent(Guid.NewGuid(), new GapEvent(x), new())).ToArray(),
            cancellationToken
        );

        async Task Commit(string id, ulong position) {
            var committed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var handler = new CheckpointCommitHandler(id, (checkpoint, _, _) => {
                committed.TrySetResult();
                return new ValueTask<Checkpoint>(checkpoint);
            }, TimeSpan.FromMilliseconds(10));
            (await handler.Commit(new(position, 0, DateTime.UtcNow), cancellationToken).NoContext()).ShouldBeTrue();
            await committed.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).NoContext();
        }
    }

    public record GapEvent(int Value);

    sealed class TailQueryCounter : IAsyncDisposable {
        readonly SqliteConnection _connection;
        readonly sqlite3 _handle;
        int _count;

        TailQueryCounter(SqliteConnection connection, string commandText) {
            _connection = connection;
            _handle = _connection.Handle!;
            // SQLitePCLRaw is already a transitive dependency. The native profile callback
            // fires on statement execution completion, not preparation or source callbacks.
            raw.sqlite3_profile(_handle, (strdelegate_profile)((_, sql, _) => {
                if (sql == commandText) Interlocked.Increment(ref _count);
            }), null);
        }

        public static async Task<TailQueryCounter> Create(string connectionString, string commandText, CancellationToken cancellationToken) {
            var connection = new SqliteConnection(connectionString);
            try {
                await connection.OpenAsync(cancellationToken).NoContext();
                var counter = new TailQueryCounter(connection, commandText);
                // Production opens/closes connections sequentially and reuses this native handle.
                await connection.CloseAsync().NoContext();
                return counter;
            } catch {
                SqliteConnection.ClearPool(connection);
                await connection.DisposeAsync().NoContext();
                throw;
            }
        }

        public int Count => Volatile.Read(ref _count);

        public async ValueTask DisposeAsync() {
            try {
                await _connection.OpenAsync().NoContext();
                _connection.Handle.ShouldBeSameAs(_handle);
                raw.sqlite3_profile(_handle, (strdelegate_profile)null!, null);
            } finally {
                // Clear only this test's uniquely named pool, never other tests' pools.
                SqliteConnection.ClearPool(_connection);
                await _connection.DisposeAsync().NoContext();
            }
        }
    }
}
