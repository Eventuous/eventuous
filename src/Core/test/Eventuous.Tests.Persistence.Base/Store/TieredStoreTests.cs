using AutoFixture;
using DotNet.Testcontainers.Containers;
using Eventuous.Tests.Persistence.Base.Fixtures;
using JetBrains.Annotations;

namespace Eventuous.Tests.Persistence.Base.Store;

public abstract class TieredStoreTestsBase<TContainer> where TContainer : DockerContainer {
    protected async Task Should_load_hot_and_archive() {
        const int count = 100;

        var store      = _storeFixture.EventStore;
        var archive    = new ArchiveStore(_storeFixture.EventStore);
        var testEvents = _fixture.CreateMany<TestEventForTiers>(count).ToArray();
        var stream     = new StreamName($"Test-{Guid.NewGuid():N}");

        await store.Store(stream, ExpectedStreamVersion.NoStream, testEvents);
        await archive.Store(stream, ExpectedStreamVersion.NoStream, testEvents);

        await store.TruncateStream(stream, new(50), ExpectedStreamVersion.Any);
        var combined = new TieredEventReader(store, archive);
        var loaded   = (await combined.ReadStream(stream, StreamReadPosition.Start)).ToArray();

        var actual = loaded.Select(x => (TestEventForTiers)x.Payload!).ToArray();
        await Assert.That(actual).IsEquivalentTo(testEvents);

        await Assert.That(loaded.Take(50).Select(x => x.FromArchive)).DoesNotContain(false);
        await Assert.That(loaded.Skip(50).Select(x => x.FromArchive)).DoesNotContain(true);
    }

    readonly Fixture                      _fixture = new();
    readonly StoreFixtureBase<TContainer> _storeFixture;

    protected TieredStoreTestsBase(StoreFixtureBase<TContainer> storeFixture) {
        _storeFixture = storeFixture;
        _storeFixture.TypeMapper.AddType<TestEventForTiers>(TestEventForTiers.TypeName);
    }

    class ArchiveStore(IEventStore original) : IEventReader, IEventWriter {
        public Task<StreamEvent[]> ReadEvents(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken)
            => original.ReadEvents(GetArchiveStreamName(stream), start, count, cancellationToken);

        public Task<StreamEvent[]> ReadEventsBackwards(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken)
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
}
