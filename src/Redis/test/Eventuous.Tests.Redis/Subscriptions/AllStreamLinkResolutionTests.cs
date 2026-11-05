using System.Globalization;
using System.Reflection;
using Eventuous.Redis.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Filters;
using Shouldly;
using StackExchange.Redis;
using Eventuous.Redis;
using static Eventuous.Redis.EventuousRedisKeys;

namespace Eventuous.Tests.Redis.Subscriptions;

/// <summary>
/// How the $all subscription resolves a link in <c>_all</c> to the entry in its source stream. The database is a proxy
/// that records the calls, so no server is involved.
/// </summary>
public class AllStreamLinkResolutionTests {
    /// <summary>
    /// Each link names one entry id. Reading from that id with no upper bound pulled the whole rest of the source
    /// stream for every linked event and kept only the first entry.
    /// </summary>
    [Test]
    public async Task Resolves_each_link_by_reading_only_the_linked_entry() {
        const string stream = "booking-1";

        var database = RecordingDatabase.Create(
            [Link(stream, "1000-0"), Link(stream, "1001-0")],
            [Entry("1000-0"), Entry("1001-0")]
        );

        var events = await new Probe().Read(database, 0);

        events.Select(x => x.StreamPosition).ShouldBe([10000L, 10010L]);

        ((RecordingDatabase)(object)database).Ranges
            .Select(r => $"{r.Key} {r.MinId} {r.MaxId} {r.Count}")
            .ShouldBe([$"{stream} 1000-0 1000-0 1", $"{stream} 1001-0 1001-0 1"], "each range must start and end at the linked entry");
    }

    static StreamEntry Link(string stream, string position) => new(RedisValue.Null, [new(EventuousRedisKeys.Stream, stream), new(Position, position)]);

    static StreamEntry Entry(string id)
        => new(
            id,
            [
                new(MessageId, Guid.NewGuid().ToString()),
                new(MessageType, "test-event"),
                new(JsonData, "{}"),
                new(JsonMetadata, "{}"),
                new(Created, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture))
            ]
        );

    sealed class Probe()
        : RedisAllStreamSubscription(
            () => null!,
            new() { SubscriptionId = "redis-all-links" },
            new NoOpCheckpointStore(),
            new ConsumePipe(),
            null
        ) {
        public Task<ReceivedEvent[]> Read(IDatabase database, long position) => ReadEvents(database, position);
    }

    public record Range(RedisKey Key, RedisValue? MinId, RedisValue? MaxId, int? Count);

    /// <summary>
    /// Serves <c>_all</c> reads from the given links and range reads from the given entries by id, recording each range read.
    /// </summary>
    public class RecordingDatabase : DispatchProxy {
        StreamEntry[] _links   = [];
        StreamEntry[] _entries = [];

        public List<Range> Ranges { get; } = [];

        public static IDatabase Create(StreamEntry[] links, StreamEntry[] entries) {
            var proxy = Create<IDatabase, RecordingDatabase>();
            var self  = (RecordingDatabase)(object)proxy;
            self._links   = links;
            self._entries = entries;

            return proxy;
        }

        protected override object? Invoke(MethodInfo? method, object?[]? args) {
            switch (method?.Name) {
                case nameof(IDatabaseAsync.StreamReadAsync):
                    return Task.FromResult(_links);
                case nameof(IDatabaseAsync.StreamRangeAsync): {
                    var range = new Range((RedisKey)args![0]!, (RedisValue?)args[1], (RedisValue?)args[2], (int?)args[3]);
                    Ranges.Add(range);

                    // Mimic XRANGE: entries from minId, up to maxId when given, at most count
                    var result = _entries
                        .Where(x => string.CompareOrdinal(x.Id, range.MinId ?? "-") >= 0)
                        .Where(x => range.MaxId is null || string.CompareOrdinal(x.Id, range.MaxId) <= 0)
                        .Take(range.Count ?? int.MaxValue)
                        .ToArray();

                    return Task.FromResult(result);
                }
                default: throw new NotSupportedException(method?.Name);
            }
        }
    }
}
