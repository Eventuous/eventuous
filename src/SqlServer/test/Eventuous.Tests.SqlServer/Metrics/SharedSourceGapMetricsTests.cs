// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eventuous.SqlServer;
using Eventuous.SqlServer.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Registrations;
using Eventuous.Tests.SqlServer.Fixtures;
using Eventuous.Tools;
using Microsoft.Data.SqlClient.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Eventuous.Tests.SqlServer.Metrics;

[NotInParallel]
public class SharedSourceGapMetricsTests {
    [Test]
    public async Task All_stream_subscriptions_read_shared_tail_once_per_collection(CancellationToken cancellationToken) {
        await using var container = SqlContainer.Create();
        await container.StartAsync(cancellationToken).NoContext();
        var connectionString = container.GetConnectionString();
        var schema = new Schema($"metrics_{Guid.NewGuid():N}");
        var storeOptions = new SqlServerStoreOptions {
            ConnectionString = connectionString, Schema = schema.SchemaName, InitializeDatabase = true
        };
        // Use the same startup retry as the metrics fixture: login readiness can precede tempdb readiness.
        await new SchemaInitializer(storeOptions).StartAsync(cancellationToken).NoContext();
        var mapper = new TypeMapper();
        mapper.AddType<GapEvent>("shared-gap-event");
        var store = new SqlServerStore(storeOptions, new DefaultEventSerializer(new(), mapper));
        // Global positions start at zero: appending eleven events leaves the tail at ten.
        await Append(11).NoContext();

        var services = new ServiceCollection();
        foreach (var id in new[] { "first", "second", "caught-up" }) {
            services.AddSubscription<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions>(id, builder => builder
                .Configure(options => {
                    options.ConnectionString = connectionString;
                    options.Schema = schema.SchemaName;
                })
                .UseCheckpointStore<NoOpCheckpointStore>());
        }
        await using var provider = services.BuildServiceProvider();
        var subscriptions = provider.GetServices<SqlServerAllStreamSubscription>().ToArray();
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

        // Count successful real SqlClient executions, not callbacks or source-key comparisons.
        // The unique schema excludes setup, appends and unrelated tests' SQL commands.
        using var queries = new TailQueryCounter($"SELECT MAX(GlobalPosition) FROM {schema.SchemaName}.Messages");
        listener.RecordObservableInstruments();
        gaps.Count.ShouldBe(3);
        gaps["first"].ShouldBe(7);
        gaps["second"].ShouldBe(3);
        gaps["caught-up"].ShouldBe(0);
        queries.Count.ShouldBe(1);

        await Append(10).NoContext();
        await Commit("second", 12).NoContext();
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

    sealed class TailQueryCounter : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable {
        readonly string _commandText;
        readonly IDisposable _allListeners;
        readonly List<IDisposable> _subscriptions = [];
        int _count;

        public TailQueryCounter(string commandText) {
            _commandText = commandText;
            _allListeners = DiagnosticListener.AllListeners.Subscribe(this);
        }

        public int Count => Volatile.Read(ref _count);

        public void OnNext(DiagnosticListener listener) {
            if (listener.Name == "SqlClientDiagnosticListener")
                _subscriptions.Add(listener.Subscribe(this, name => name == "Microsoft.Data.SqlClient.WriteCommandAfter"));
        }

        public void OnNext(KeyValuePair<string, object?> value) {
            if (value.Value is SqlClientCommandAfter after && after.Command.CommandText == _commandText)
                Interlocked.Increment(ref _count);
        }

        public void OnCompleted() { }
        public void OnError(Exception error) { }

        public void Dispose() {
            _allListeners.Dispose();
            foreach (var subscription in _subscriptions) subscription.Dispose();
        }
    }
}
