// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.Metrics;
using Eventuous.Sqlite.Projections;
using Eventuous.Sqlite.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Eventuous.Tests.Sqlite.Subscriptions;

public class EndOfStreamSourceKeyTests {
    [Test]
    public async Task Keys_use_effective_connection_schema_and_stream() {
        await using var first = All("first", "connection", "schema");
        await using var same = All("second", "connection", "schema");
        await using var otherConnection = All("third", "other", "schema");
        await using var otherSchema = All("fourth", "connection", "other");
        await using var stream = Stream("stream1", "one");
        await using var sameStream = Stream("stream2", "one");
        await using var otherStream = Stream("stream3", "two");
        await using var overridden = new SqliteAllStreamSubscription(
            new() { SubscriptionId = "override", ConnectionString = "ignored", Schema = "ignored" },
            new NoOpCheckpointStore(), new(), connectionOptions: new SqliteConnectionOptions("connection", "schema")
        );

        first.EndOfStreamSourceKey.ShouldBe(same.EndOfStreamSourceKey);
        first.EndOfStreamSourceKey.ShouldBe(overridden.EndOfStreamSourceKey);
        first.EndOfStreamSourceKey.ShouldNotBe(otherConnection.EndOfStreamSourceKey);
        first.EndOfStreamSourceKey.ShouldNotBe(otherSchema.EndOfStreamSourceKey);
        first.EndOfStreamSourceKey.ShouldNotBe(stream.EndOfStreamSourceKey);
        stream.EndOfStreamSourceKey.ShouldBe(sameStream.EndOfStreamSourceKey);
        stream.EndOfStreamSourceKey.ShouldNotBe(otherStream.EndOfStreamSourceKey);
    }

    [Test]
    [NotInParallel]
    public async Task Existing_custom_queries_keep_independent_measurements() {
        var registrations = new ServiceCollection();
        registrations.AddSingleton<ICheckpointStore, NoOpCheckpointStore>();
        foreach (var id in new[] { "custom-a", "custom-b" })
            registrations.AddSubscription<CustomQuery, SqliteAllStreamSubscriptionOptions>(id, builder =>
                builder.Configure(options => { options.ConnectionString = "Data Source=:memory:"; options.Schema = "events"; }));
        await using var services = registrations.BuildServiceProvider();
        using var metrics = new SubscriptionMetrics(services.GetServices<GetSubscriptionEndOfStream>());
        Dictionary<string, long> values = new();
        using var listener = new MeterListener {
            InstrumentPublished = (instrument, owner) => {
                if (instrument.Meter.Name == SubscriptionMetrics.MeterName && instrument.Name == SubscriptionMetrics.GapCountMetricName)
                    owner.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
            values[(string)tags.ToArray().Single(x => x.Key == SubscriptionMetrics.SubscriptionIdTag).Value!] = value);
        listener.Start();
        listener.RecordObservableInstruments();
        values["custom-a"].ShouldBe(10);
        values["custom-b"].ShouldBe(20);
    }

    [Test]
    public async Task Derived_subscriptions_are_independent_unless_they_opt_in() {
        await using var all = new CustomQuery(new() { SubscriptionId = "custom-a", ConnectionString = "connection" }, new NoOpCheckpointStore(), new());
        await using var stream = new DerivedStream();
        await using var optedIn = new OptedInQuery(new() { SubscriptionId = "custom-b", ConnectionString = "connection" }, new NoOpCheckpointStore(), new());
        ((IMeasuredSubscription)all).EndOfStreamSourceKey.ShouldBeNull();
        ((IMeasuredSubscription)stream).EndOfStreamSourceKey.ShouldBeNull();
        ((IMeasuredSubscription)optedIn).EndOfStreamSourceKey.ShouldBe("explicit-custom-source");
    }

    public class CustomQuery(SqliteAllStreamSubscriptionOptions options, ICheckpointStore checkpoints, ConsumePipe pipe)
        : SqliteAllStreamSubscription(options, checkpoints, pipe) {
        protected override SqliteCommand PrepareEndOfStreamCommand(SqliteConnection connection) {
            var command = connection.CreateCommand();
            command.CommandText = SubscriptionId == "custom-a" ? "SELECT 10" : "SELECT 20";
            return command;
        }
    }

    sealed class OptedInQuery(SqliteAllStreamSubscriptionOptions options, ICheckpointStore checkpoints, ConsumePipe pipe)
        : CustomQuery(options, checkpoints, pipe) {
        public override object EndOfStreamSourceKey => "explicit-custom-source";
    }

    sealed class DerivedStream() : SqliteStreamSubscription(
        new() { SubscriptionId = "derived", ConnectionString = "connection", Stream = new("stream") }, new NoOpCheckpointStore(), new());

    static SqliteAllStreamSubscription All(string id, string connection, string schema)
        => new(new() { SubscriptionId = id, ConnectionString = connection, Schema = schema }, new NoOpCheckpointStore(), new());

    static SqliteStreamSubscription Stream(string id, string stream)
        => new(new() { SubscriptionId = id, ConnectionString = "connection", Schema = "schema", Stream = new(stream) }, new NoOpCheckpointStore(), new());
}
