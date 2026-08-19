using Bogus;
using DotNet.Testcontainers.Containers;
using Eventuous.Tests.Persistence.Base.Fixtures;
using JetBrains.Annotations;

namespace Eventuous.Tests.Persistence.Base.Store;

public abstract class TieredStoreTestsBase<TContainer> where TContainer : DockerContainer {
    protected async Task Should_load_hot_and_archive() {
        const int count = 100;

        var (combined, stream, testEvents) = await SeedTieredStream(count, truncateHotAt: 50);

        var loaded = (await combined.ReadStream(stream, StreamReadPosition.Start)).ToArray();

        var actual = loaded.Select(x => (TestEventForTiers)x.Payload!);
        await Assert.That(actual).IsEquivalentTo(testEvents);

        await Assert.That(loaded.Take(50).Select(x => x.FromArchive)).DoesNotContain(false);
        await Assert.That(loaded.Skip(50).Select(x => x.FromArchive)).DoesNotContain(true);
    }

    protected async Task Should_read_bounded_count_across_tier_boundary() {
        const int count = 100;

        var (combined, stream, testEvents) = await SeedTieredStream(count, truncateHotAt: 50);

        // The first 50 events only exist in the archive, the hot store starts at revision 50
        var firstPage = await combined.ReadEvents(stream, StreamReadPosition.Start, 50, true, CancellationToken.None);

        await Assert.That(firstPage.Length).IsEqualTo(50);
        await Assert.That(firstPage.Select(x => (TestEventForTiers)x.Payload!)).IsEquivalentTo(testEvents.Take(50));

        var loaded = new List<StreamEvent>();

        await foreach (var evt in combined.ReadStreamToEnd(stream, StreamReadPosition.Start, pageSize: 50)) {
            loaded.Add(evt);
        }

        await Assert.That(loaded.Select(x => (TestEventForTiers)x.Payload!)).IsEquivalentTo(testEvents);
    }

    protected async Task Should_return_empty_reading_past_end() {
        const int count = 10;

        var (tieredReader, stream, _) = await SeedTieredStream(count);

        var loaded = await tieredReader.ReadEvents(stream, new(count), 5, true, CancellationToken.None);

        await Assert.That(loaded).IsEmpty();
    }

    protected async Task Should_read_stream_to_end_with_exact_page_multiple() {
        const int count = 100;

        var (tieredReader, stream, testEvents) = await SeedTieredStream(count);

        var loaded = new List<StreamEvent>();

        // 100 events with page size 50 forces a final read past the stream end
        await foreach (var evt in tieredReader.ReadStreamToEnd(stream, StreamReadPosition.Start, pageSize: 50)) {
            loaded.Add(evt);
        }

        var actual = loaded.Select(x => (TestEventForTiers)x.Payload!);
        await Assert.That(actual).IsEquivalentTo(testEvents);
    }

    protected async Task Should_read_backwards_more_than_available() {
        const int count = 10;

        var (combined, stream, testEvents) = await SeedTieredStream(count);

        // Requesting more events than the stream holds reads the hot store down to revision 0
        var loaded = await combined.ReadEventsBackwards(stream, StreamReadPosition.End, count * 2, true, CancellationToken.None);

        await Assert.That(loaded.Select(x => (TestEventForTiers)x.Payload!).Reverse()).IsEquivalentTo(testEvents);
    }

    async Task<(TieredEventReader Reader, StreamName Stream, TestEventForTiers[] Events)> SeedTieredStream(int count, long? truncateHotAt = null) {
        var store      = _storeFixture.EventStore;
        var archive    = new ArchiveStore(_storeFixture.EventStore);
        var testEvents = TestEventForTiers.CreateMany(count).ToArray();
        var stream     = new StreamName($"Test-{Guid.NewGuid():N}");

        await store.Store(stream, ExpectedStreamVersion.NoStream, testEvents);
        await archive.Store(stream, ExpectedStreamVersion.NoStream, testEvents);

        if (truncateHotAt != null) {
            await store.TruncateStream(stream, new(truncateHotAt.Value), ExpectedStreamVersion.Any);
        }

        return (new(store, archive), stream, testEvents);
    }

    readonly StoreFixtureBase<TContainer> _storeFixture;

    protected TieredStoreTestsBase(StoreFixtureBase<TContainer> storeFixture) {
        _storeFixture = storeFixture;
        _storeFixture.TypeMapper.AddType<TestEventForTiers>(TestEventForTiers.TypeName);
    }

    class ArchiveStore(IEventStore original) : IEventReader, IEventWriter {
        public IAsyncEnumerable<StreamEvent> ReadEvents(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken)
            => original.ReadEvents(GetArchiveStreamName(stream), start, count, cancellationToken);

        public IAsyncEnumerable<StreamEvent> ReadEventsBackwards(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken)
            => original.ReadEventsBackwards(GetArchiveStreamName(stream), start, count, cancellationToken);

        static StreamName GetArchiveStreamName(string streamName) => new($"Archive-{streamName}");

        public Task<AppendEventsResult> AppendEvents(
                StreamName                          stream,
                ExpectedStreamVersion               expectedVersion,
                IReadOnlyCollection<NewStreamEvent> events,
                CancellationToken                   cancellationToken
            )
            => original.AppendEvents(GetArchiveStreamName(stream), expectedVersion, events, cancellationToken);
    }
}

[UsedImplicitly]
record TestEventForTiers(string Data, int Number) {
    public const string TypeName = "test-event-tiers";

    static readonly Faker<TestEventForTiers> Faker = new Faker<TestEventForTiers>().CustomInstantiator(f => new(f.Commerce.Product(), f.Random.Int()));

    public static IEnumerable<TestEventForTiers> CreateMany(int count) => Faker.Generate(count);
}
