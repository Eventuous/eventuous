// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using System.Text.Json;
using Eventuous.Sqlite.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Microsoft.Data.Sqlite;
using Shouldly;

namespace Eventuous.Tests.Sqlite.Subscriptions;

/// <summary>
/// How <c>SqlSubscriptionBase</c> turns a polled row into a consume context: which metadata serializer it uses and
/// what a metadata failure does to the poll loop. Sqlite shares that base with Postgres and SQL Server but needs no
/// container, so it is the cheapest place to pin it.
/// </summary>
/// <remarks>The poll query selects literal rows from an in-memory database, so no schema or store is involved.</remarks>
public class MetadataDeserializationTests {
    const string KnownType   = "sql-meta-test-event";
    const string UnknownType = "sql-meta-test-unknown";

    // Multibyte characters on purpose: the payload and metadata are transcoded from strings to UTF-8 bytes.
    const string Text = "héllo wörld ☕ 🌍";

    /// <summary>
    /// The serializer given to the subscription is the one that reads metadata; it used to be ignored in favour of the default.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task Uses_the_configured_metadata_serializer(CancellationToken ct) {
        var subscription = new ScriptedSubscription(
            "sql-meta-configured",
            [Row(1, KnownType, $$"""{"text":"{{Text}}"}""", $$"""{"note":"{{Text}}"}""")],
            new MarkingMetadataSerializer()
        );

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);
        var received = await WaitUntil(() => subscription.Collector.Contexts.Count == 1, TimeSpan.FromSeconds(10));
        await subscription.Unsubscribe(_ => { }, ct);

        received.ShouldBeTrue("the event should have reached the handler");
        var context = subscription.Collector.Contexts.Single();
        context.Message.ShouldBeOfType<TestEvent>().Text.ShouldBe(Text);
        context.Metadata.ShouldNotBeNull();
        context.Metadata.GetString(MarkingMetadataSerializer.Key).ShouldBe(MarkingMetadataSerializer.Value, "the configured serializer should have read the metadata");
        context.Metadata.GetString("note").ShouldBe(Text);
    }

    /// <summary>
    /// Without <c>ThrowOnError</c> a malformed metadata row is logged and skipped; it must not fault the poll loop,
    /// which would otherwise fail on the same row after every resubscribe.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task Malformed_metadata_does_not_drop_the_subscription(CancellationToken ct) {
        var subscription = new ScriptedSubscription("sql-meta-malformed", [Row(1, KnownType, $$"""{"text":"{{Text}}"}""", "not json")]);

        var drops = 0;
        await subscription.Subscribe(_ => { }, (_, _, _) => Interlocked.Increment(ref drops), ct);
        var received = await WaitUntil(() => subscription.Collector.Contexts.Count == 1, TimeSpan.FromSeconds(10));
        await subscription.Unsubscribe(_ => { }, ct);

        received.ShouldBeTrue("the event should still reach the handler");
        subscription.Collector.Contexts.Single().Metadata.ShouldBeNull();
        drops.ShouldBe(0);
    }

    /// <summary>
    /// Under <c>ThrowOnError</c> malformed metadata is still a fault, reported as a deserialization failure.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task Malformed_metadata_drops_the_subscription_under_ThrowOnError(CancellationToken ct) {
        var subscription = new ScriptedSubscription(
            "sql-meta-malformed-throw",
            [Row(1, KnownType, $$"""{"text":"{{Text}}"}""", "not json")],
            throwOnError: true
        );

        Exception? reported = null;
        await subscription.Subscribe(_ => { }, (_, _, e) => reported ??= e, ct);
        var dropped = await WaitUntil(() => reported != null, TimeSpan.FromSeconds(10));
        await subscription.Unsubscribe(_ => { }, ct);

        dropped.ShouldBeTrue("malformed metadata under ThrowOnError should drop the subscription");
        reported.ShouldBeOfType<DeserializationException>();
        subscription.Collector.Contexts.ShouldBeEmpty();
    }

    /// <summary>
    /// An empty metadata string and the JSON literal <c>null</c> both mean "no metadata", not a failure, so neither
    /// faults the subscription even under <c>ThrowOnError</c>. The empty string used to throw.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task Empty_and_null_metadata_give_no_metadata(CancellationToken ct) {
        var subscription = new ScriptedSubscription(
            "sql-meta-empty",
            [
                Row(1, KnownType, $$"""{"text":"{{Text}}"}""", ""),
                Row(2, KnownType, $$"""{"text":"{{Text}}"}""", "null")
            ],
            throwOnError: true
        );

        var drops = 0;
        await subscription.Subscribe(_ => { }, (_, _, _) => Interlocked.Increment(ref drops), ct);
        var received = await WaitUntil(() => subscription.Collector.Contexts.Count == 2, TimeSpan.FromSeconds(10));
        await subscription.Unsubscribe(_ => { }, ct);

        received.ShouldBeTrue("both events should reach the handler");
        subscription.Collector.Contexts.ShouldAllBe(x => x.Metadata == null);
        drops.ShouldBe(0);
    }

    /// <summary>
    /// An event whose payload can't be deserialized is acknowledged without being handled, so its metadata is never read,
    /// and malformed metadata on it doesn't fault the subscription even under <c>ThrowOnError</c>.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task Metadata_of_a_payload_less_event_is_not_read(CancellationToken ct) {
        var metaSerializer = new MarkingMetadataSerializer();

        var subscription = new ScriptedSubscription(
            "sql-meta-payload-less",
            [
                Row(1, UnknownType, """{"text":"unknown"}""", "not json"),
                Row(2, KnownType, $$"""{"text":"{{Text}}"}""", "{}")
            ],
            metaSerializer,
            throwOnError: true
        );

        var drops = 0;
        await subscription.Subscribe(_ => { }, (_, _, _) => Interlocked.Increment(ref drops), ct);
        var received = await WaitUntil(() => subscription.Collector.Contexts.Count == 1, TimeSpan.FromSeconds(10));
        await subscription.Unsubscribe(_ => { }, ct);

        received.ShouldBeTrue("the event after the payload-less one should reach the handler");
        subscription.Collector.Contexts.Single().Message.ShouldBeOfType<TestEvent>();
        drops.ShouldBe(0);
        metaSerializer.Calls.ShouldBe(1, "only the handled event's metadata should be deserialized");
    }

    record EventRow(long Position, string Type, string Data, string Meta);

    static EventRow Row(long position, string type, string data, string meta) => new(position, type, data, meta);

    static async Task<bool> WaitUntil(Func<bool> condition, TimeSpan timeout) {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline) {
            if (condition()) return true;

            await Task.Delay(20);
        }

        return condition();
    }

    record TestEvent(string Text);

    static IEventSerializer CreateSerializer() {
        var typeMapper = new TypeMapper();
        typeMapper.AddType<TestEvent>(KnownType);

        return new DefaultEventSerializer(new(JsonSerializerDefaults.Web), typeMapper);
    }

    /// <summary>
    /// Polls literal rows instead of the messages table, returning those past the subscription's position.
    /// </summary>
    sealed class ScriptedSubscription(string id, EventRow[] rows, IMetadataSerializer? metaSerializer = null, bool throwOnError = false)
        : SqliteAllStreamSubscription(
            new() {
                SubscriptionId   = id,
                ConnectionString = "Data Source=:memory:",
                ThrowOnError     = throwOnError,
                Polling          = new() { MinIntervalMs = 1, MaxIntervalMs = 20 }
            },
            new NoOpCheckpointStore(),
            new ConsumePipe().AddDefaultConsumer(Collectors[id] = new()),
            eventSerializer: CreateSerializer(),
            metaSerializer: metaSerializer
        ) {
        public CollectingHandler Collector => Collectors[id];

        protected override SqliteCommand PrepareCommand(SqliteConnection connection, long start) {
            var cmd = connection.CreateCommand();

            var values = string.Join(
                " UNION ALL ",
                rows.Select((_, i) => $"SELECT @id{i}, @type{i}, @pos{i}, @pos{i}, @data{i}, @meta{i}, '2026-01-01 00:00:00', 'sql-meta-stream'")
            );

            cmd.CommandText = $"WITH events(id, type, sp, gp, data, meta, created, stream) AS ({values}) SELECT * FROM events WHERE gp > @start ORDER BY gp";
            cmd.Parameters.AddWithValue("@start", start);

            for (var i = 0; i < rows.Length; i++) {
                cmd.Parameters.AddWithValue($"@id{i}", Guid.NewGuid().ToString());
                cmd.Parameters.AddWithValue($"@type{i}", rows[i].Type);
                cmd.Parameters.AddWithValue($"@pos{i}", rows[i].Position);
                cmd.Parameters.AddWithValue($"@data{i}", rows[i].Data);
                cmd.Parameters.AddWithValue($"@meta{i}", rows[i].Meta);
            }

            return cmd;
        }
    }

    // The handler has to exist before the base constructor runs, so it is handed over through this map.
    static readonly ConcurrentDictionary<string, CollectingHandler> Collectors = new();

    sealed class CollectingHandler : BaseEventHandler {
        public ConcurrentQueue<IMessageConsumeContext> Contexts { get; } = new();

        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
            Contexts.Enqueue(context);

            return new(EventHandlingStatus.Success);
        }
    }

    /// <summary>
    /// Reads metadata like the default serializer, then marks it so a test can tell which serializer ran.
    /// </summary>
    sealed class MarkingMetadataSerializer : IMetadataSerializer {
        public const string Key   = "serializer";
        public const string Value = "custom";

        int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public byte[] Serialize(Metadata evt) => DefaultMetadataSerializer.Instance.Serialize(evt);

        public Metadata? Deserialize(ReadOnlySpan<byte> bytes) {
            Interlocked.Increment(ref _calls);

            return DefaultMetadataSerializer.Instance.Deserialize(bytes)?.With(Key, Value);
        }
    }
}
