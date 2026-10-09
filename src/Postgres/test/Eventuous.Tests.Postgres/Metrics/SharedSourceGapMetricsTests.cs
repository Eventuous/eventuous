// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

extern alias Postgres;

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.Postgresql;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Registrations;
using Eventuous.Tests.Postgres.Fixtures;
using Microsoft.Extensions.DependencyInjection;

// Select the store helper explicitly: Persistence exposes the same internal extensions.
using static Postgres::Eventuous.Tools.TaskExtensions;

namespace Eventuous.Tests.Postgres.Metrics;

[NotInParallel]
public class SharedSourceGapMetricsTests {
    [Test]
    [Arguments(ServiceLifetime.Singleton)]
    [Arguments(ServiceLifetime.Transient)]
    public async Task All_stream_subscriptions_read_shared_tail_once_per_collection(ServiceLifetime dataSourceLifetime, CancellationToken cancellationToken) {
        await using var container = PostgresContainer.Create();
        await container.StartAsync(cancellationToken).NoContext();
        var connectionString = container.GetConnectionString();
        var schema = new Schema($"metrics_{Guid.NewGuid():N}");
        var storeOptions = new PostgresStoreOptions {
            ConnectionString = connectionString, Schema = schema.Name, InitializeDatabase = true
        };
        await new SchemaInitializer(storeOptions).StartAsync(cancellationToken).NoContext();
        var mapper = new TypeMapper();
        mapper.AddType<GapEvent>("shared-gap-event");
        using var queries = new TailQueryCounter($"select max(global_position) from {schema.Name}.messages");
        var services = new ServiceCollection();
        services.AddEventuousPostgres(connectionString, schema.Name, dataSourceLifetime: dataSourceLifetime);
        foreach (var id in new[] { "first", "second", "caught-up" }) {
            services.AddSubscription<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions>(id, builder => builder
                .Configure(options => {
                    options.Schema = schema.Name;
                })
                .UseCheckpointStore<NoOpCheckpointStore>());
        }
        await using var provider = services.BuildServiceProvider();
        var store = new PostgresStore(provider.GetRequiredService<Npgsql.NpgsqlDataSource>(), storeOptions,
            new DefaultEventSerializer(new(), mapper));
        // PostgreSQL global positions start at one: ten events leave the tail at ten.
        await Append(10).NoContext();
        var subscriptions = provider.GetServices<PostgresAllStreamSubscription>().ToArray();
        await Assert.That(subscriptions.Length).IsEqualTo(3);
        await Assert.That(subscriptions.Distinct().Count()).IsEqualTo(3);
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

        // Count completed production Npgsql commands, not callbacks or source-key comparisons.
        // Exact SQL with a unique schema excludes setup, appends and unrelated queries.
        await Assert.That(queries.Count).IsEqualTo(0);
        listener.RecordObservableInstruments();
        await Assert.That(gaps.Count).IsEqualTo(3);
        await Assert.That(gaps["first"]).IsEqualTo(7);
        await Assert.That(gaps["second"]).IsEqualTo(3);
        await Assert.That(gaps["caught-up"]).IsEqualTo(0);
        await Assert.That(queries.Count).IsEqualTo(1);
        await Assert.That(queries.FailedCount).IsEqualTo(0);

        await Append(10).NoContext();
        await Commit("second", 12).NoContext();
        gaps.Clear();
        listener.RecordObservableInstruments();
        await Assert.That(gaps.Count).IsEqualTo(3);
        await Assert.That(gaps["first"]).IsEqualTo(17);
        await Assert.That(gaps["second"]).IsEqualTo(8);
        await Assert.That(gaps["caught-up"]).IsEqualTo(10);
        await Assert.That(queries.Count).IsEqualTo(2);
        await Assert.That(queries.FailedCount).IsEqualTo(0);

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
            await Assert.That(await handler.Commit(new(position, 0, DateTime.UtcNow), cancellationToken).NoContext()).IsTrue();
            await committed.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).NoContext();
        }
    }

    public record GapEvent(int Value);

    sealed class TailQueryCounter : IDisposable {
        readonly ActivityListener _listener;
        int _count;
        int _failedCount;

        public TailQueryCounter(string commandText) {
            _listener = new ActivityListener {
                ShouldListenTo = source => source.Name == "Npgsql",
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                SampleUsingParentId = (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity => {
                    if (!Equals(activity.GetTagItem("db.query.text"), commandText)) return;
                    // Count only completed, non-error production commands; also reject any failures.
                    if (activity.Status == ActivityStatusCode.Error)
                        Interlocked.Increment(ref _failedCount);
                    else
                        Interlocked.Increment(ref _count);
                }
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public int Count => Volatile.Read(ref _count);
        public int FailedCount => Volatile.Read(ref _failedCount);

        public void Dispose() => _listener.Dispose();
    }
}
