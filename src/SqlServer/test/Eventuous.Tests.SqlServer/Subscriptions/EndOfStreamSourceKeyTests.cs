// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.SqlServer.Projections;
using Eventuous.SqlServer.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Shouldly;

namespace Eventuous.Tests.SqlServer.Subscriptions;

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
        await using var overridden = new SqlServerAllStreamSubscription(
            new() { SubscriptionId = "override", ConnectionString = "ignored", Schema = "ignored" },
            new NoOpCheckpointStore(), new(), connectionOptions: new SqlServerConnectionOptions("connection", "schema")
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
    public async Task Derived_subscriptions_keep_independent_reads() {
        await using var all = new DerivedAll();
        await using var stream = new DerivedStream();
        ((IMeasuredSubscription)all).EndOfStreamSourceKey.ShouldBeNull();
        ((IMeasuredSubscription)stream).EndOfStreamSourceKey.ShouldBeNull();
    }

    sealed class DerivedAll() : SqlServerAllStreamSubscription(
        new() { SubscriptionId = "derived-all", ConnectionString = "connection" }, new NoOpCheckpointStore(), new());

    sealed class DerivedStream() : SqlServerStreamSubscription(
        new() { SubscriptionId = "derived-stream", ConnectionString = "connection", Stream = new("one") }, new NoOpCheckpointStore(), new());

    static SqlServerAllStreamSubscription All(string id, string connection, string schema)
        => new(new() { SubscriptionId = id, ConnectionString = connection, Schema = schema }, new NoOpCheckpointStore(), new());

    static SqlServerStreamSubscription Stream(string id, string stream)
        => new(new() { SubscriptionId = id, ConnectionString = "connection", Schema = "schema", Stream = new(stream) }, new NoOpCheckpointStore(), new());
}
