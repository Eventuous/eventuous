// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Postgresql;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Eventuous.Tests.Postgres.Subscriptions;

public class EndOfStreamSourceKeyTests {
    [Test]
    public async Task Keys_separate_data_sources_schemas_and_streams() {
        await using var source = NpgsqlDataSource.Create("Host=localhost;Database=one");
        await using var otherSource = NpgsqlDataSource.Create("Host=localhost;Database=two");
        await using var first = All(source, "first", "schema");
        await using var same = All(source, "second", "schema");
        await using var other = All(otherSource, "third", "schema");
        await using var otherSchema = All(source, "fourth", "other");
        await using var overridden = new PostgresAllStreamSubscription(source,
            new() { SubscriptionId = "override", Schema = "ignored" }, new NoOpCheckpointStore(), new(), storeOptions: new("schema"));
        await using var stream = Stream(source, "stream1", "one");
        await using var sameStream = Stream(source, "stream2", "one");
        await using var otherStream = Stream(source, "stream3", "two");

        await Assert.That(first.EndOfStreamSourceKey).IsEqualTo(same.EndOfStreamSourceKey);
        await Assert.That(first.EndOfStreamSourceKey).IsEqualTo(overridden.EndOfStreamSourceKey);
        await Assert.That(first.EndOfStreamSourceKey).IsNotEqualTo(other.EndOfStreamSourceKey);
        await Assert.That(first.EndOfStreamSourceKey).IsNotEqualTo(otherSchema.EndOfStreamSourceKey);
        await Assert.That(first.EndOfStreamSourceKey).IsNotEqualTo(stream.EndOfStreamSourceKey);
        await Assert.That(stream.EndOfStreamSourceKey).IsEqualTo(sameStream.EndOfStreamSourceKey);
        await Assert.That(stream.EndOfStreamSourceKey).IsNotEqualTo(otherStream.EndOfStreamSourceKey);
    }

    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task Transient_registration_shares_only_known_equivalent_sources(bool fromConfiguration, bool customConfiguration) {
        var services = new ServiceCollection();
        // Even a matching connection string cannot prove an arbitrary callback is equivalent.
        Action<IServiceProvider, NpgsqlDataSourceBuilder>? configure = customConfiguration ? (_, _) => { } : null;
        if (fromConfiguration) {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
                ["ConnectionString"] = "Host=localhost;Database=one"
            }).Build();
            services.AddEventuousPostgres(configuration, configureBuilder: configure, dataSourceLifetime: ServiceLifetime.Transient);
        } else {
            services.AddEventuousPostgres("Host=localhost;Database=one", configureBuilder: configure, dataSourceLifetime: ServiceLifetime.Transient);
        }
        await using var provider = services.BuildServiceProvider();
        var firstSource = provider.GetRequiredService<NpgsqlDataSource>();
        var secondSource = provider.GetRequiredService<NpgsqlDataSource>();
        await Assert.That(ReferenceEquals(firstSource, secondSource)).IsFalse();
        await using var first = All(firstSource, "first", "schema");
        await using var second = All(secondSource, "second", "schema");
        await using var stream = Stream(firstSource, "stream", "one");
        await using var sameStream = Stream(secondSource, "same-stream", "one");
        await using var otherStream = Stream(secondSource, "other-stream", "two");
        await using var otherSchema = All(secondSource, "other-schema", "other");

        await Assert.That(Equals(first.EndOfStreamSourceKey, second.EndOfStreamSourceKey)).IsEqualTo(!customConfiguration);
        await Assert.That(Equals(stream.EndOfStreamSourceKey, sameStream.EndOfStreamSourceKey)).IsEqualTo(!customConfiguration);
        await Assert.That(first.EndOfStreamSourceKey).IsNotEqualTo(otherSchema.EndOfStreamSourceKey);
        await Assert.That(first.EndOfStreamSourceKey).IsNotEqualTo(stream.EndOfStreamSourceKey);
        await Assert.That(stream.EndOfStreamSourceKey).IsNotEqualTo(otherStream.EndOfStreamSourceKey);
    }

    [Test]
    public async Task Independently_created_sources_with_matching_connection_strings_remain_separate() {
        await using var source = NpgsqlDataSource.Create("Host=localhost;Database=one");
        await using var otherSource = NpgsqlDataSource.Create("Host=localhost;Database=one");
        await using var first = All(source, "first", "schema");
        await using var second = All(otherSource, "second", "schema");
        await Assert.That(first.EndOfStreamSourceKey).IsNotEqualTo(second.EndOfStreamSourceKey);
    }

    [Test]
    public async Task Derived_subscriptions_keep_independent_reads() {
        await using var source = NpgsqlDataSource.Create("Host=localhost;Database=one");
        await using var all = new DerivedAll(source);
        await using var stream = new DerivedStream(source);
        await Assert.That(((IMeasuredSubscription)all).EndOfStreamSourceKey).IsNull();
        await Assert.That(((IMeasuredSubscription)stream).EndOfStreamSourceKey).IsNull();
    }

    sealed class DerivedAll(NpgsqlDataSource source) : PostgresAllStreamSubscription(
        source, new() { SubscriptionId = "derived-all" }, new NoOpCheckpointStore(), new());

    sealed class DerivedStream(NpgsqlDataSource source) : PostgresStreamSubscription(
        source, new() { SubscriptionId = "derived-stream", Stream = new("one") }, new NoOpCheckpointStore(), new());

    static PostgresAllStreamSubscription All(NpgsqlDataSource source, string id, string schema)
        => new(source, new() { SubscriptionId = id, Schema = schema }, new NoOpCheckpointStore(), new());

    static PostgresStreamSubscription Stream(NpgsqlDataSource source, string id, string stream)
        => new(source, new() { SubscriptionId = id, Schema = "schema", Stream = new(stream) }, new NoOpCheckpointStore(), new());
}
