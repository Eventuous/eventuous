using System.Collections.Concurrent;
using System.Text.Json;
using Eventuous.Redis.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Shouldly;
using StackExchange.Redis;

namespace Eventuous.Tests.Redis.Subscriptions;

/// <summary>
/// How the Redis subscription turns a received entry into a consume context: which metadata serializer it uses and
/// what a metadata failure does to the poll loop. The entries come from the ReadEvents seam, so no server is involved.
/// </summary>
public class MetadataDeserializationTests {
    const string KnownType   = "redis-meta-test-event";
    const string UnknownType = "redis-meta-test-unknown";

    // Multibyte characters on purpose: the payload and metadata are transcoded from strings to UTF-8 bytes.
    const string Text = "héllo wörld ☕ 🌍";

    /// <summary>
    /// The serializer given to the subscription is the one that reads metadata; it used to be ignored in favour of the default.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task Uses_the_configured_metadata_serializer(CancellationToken ct) {
        var subscription = new ScriptedSubscription(
            "redis-meta-configured",
            [Event(KnownType, $$"""{"text":"{{Text}}"}""", $$"""{"note":"{{Text}}"}""")],
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
    /// Without <c>ThrowOnError</c> a malformed metadata entry is logged and skipped; it must not fault the poll loop,
    /// which would otherwise fail on the same entry after every resubscribe.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task Malformed_metadata_does_not_drop_the_subscription(CancellationToken ct) {
        var subscription = new ScriptedSubscription(
            "redis-meta-malformed",
            [Event(KnownType, $$"""{"text":"{{Text}}"}""", "not json")]
        );

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
            "redis-meta-malformed-throw",
            [Event(KnownType, $$"""{"text":"{{Text}}"}""", "not json")],
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
    /// An event whose payload can't be deserialized is acknowledged without being handled, so its metadata is never read,
    /// and malformed metadata on it doesn't fault the subscription even under <c>ThrowOnError</c>.
    /// </summary>
    [Test]
    [Timeout(30_000)]
    public async Task Metadata_of_a_payload_less_event_is_not_read(CancellationToken ct) {
        var metaSerializer = new MarkingMetadataSerializer();

        var subscription = new ScriptedSubscription(
            "redis-meta-payload-less",
            [
                Event(UnknownType, """{"text":"unknown"}""", "not json", position: 1),
                Event(KnownType, $$"""{"text":"{{Text}}"}""", "{}", position: 2)
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

    static ReceivedEvent Event(string type, string data, string? meta, long position = 1)
        => new(Guid.NewGuid(), type, position, position, data, meta, DateTime.UtcNow, "redis-meta-stream");

    static async Task<bool> WaitUntil(Func<bool> condition, TimeSpan timeout) {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline) {
            if (condition()) return true;

            await Task.Delay(20);
        }

        return condition();
    }

    record TestEvent(string Text);

    record TestOptions : RedisSubscriptionBaseOptions;

    static IEventSerializer CreateSerializer() {
        var typeMapper = new TypeMapper();
        typeMapper.AddType<TestEvent>(KnownType);

        return new DefaultEventSerializer(new(JsonSerializerDefaults.Web), typeMapper);
    }

    /// <summary>
    /// Returns the scripted entries on the first poll and nothing afterwards. The database is never touched.
    /// </summary>
    sealed class ScriptedSubscription(string id, ReceivedEvent[] events, IMetadataSerializer? metaSerializer = null, bool throwOnError = false)
        : RedisSubscriptionBase<TestOptions>(
            () => null!,
            new() { SubscriptionId = id, ThrowOnError = throwOnError },
            new NoOpCheckpointStore(),
            new ConsumePipe().AddDefaultConsumer(Handlers[id] = new()),
            SubscriptionKind.All,
            null,
            CreateSerializer(),
            metaSerializer
        ) {
        public CollectingHandler Collector => Handlers[id];

        int _polls;

        protected override async Task<ReceivedEvent[]> ReadEvents(IDatabase database, long position) {
            if (Interlocked.Increment(ref _polls) == 1) return events;

            await Task.Delay(20);

            return [];
        }
    }

    // The handler has to exist before the base constructor runs, so it is handed over through this map.
    static readonly ConcurrentDictionary<string, CollectingHandler> Handlers = new();

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
