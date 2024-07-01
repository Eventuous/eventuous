using JetBrains.Annotations;

namespace Eventuous.Tests.EventStore.Store;

public class TieredStoreTests : IAsyncLifetime {
    [Fact]
    public async Task Should_load_hot() {
        const int count = 100;

        var store      = _storeFixture.EventStore;
        var archive    = new ArchiveStore(_storeFixture.EventStore);
        var testEvents = _fixture.CreateMany<TestEvent>(count).ToList();
        var stream     = new StreamName($"Test-{Guid.NewGuid():N}");

        await store.Store(stream, ExpectedStreamVersion.NoStream, testEvents);
        await archive.Store(stream, ExpectedStreamVersion.NoStream, testEvents);

        await store.TruncateStream(stream, new(50), ExpectedStreamVersion.Any);
        var combined = new TieredEventReader(store, archive);
        var loaded   = (await combined.ReadStream(stream, StreamReadPosition.Start)).ToArray();

        var actual = loaded.Select(x => (TestEvent)x.Payload!).ToArray();
        actual.Should().BeEquivalentTo(testEvents);

        loaded.Take(50).Select(x => x.FromArchive).Should().AllSatisfy(x => x.Should().BeFalse());
        loaded.Skip(50).Select(x => x.FromArchive).Should().AllSatisfy(x => x.Should().BeTrue());
    }

    readonly StoreFixture _storeFixture = new();
    readonly Fixture      _fixture      = new();

    public async Task InitializeAsync() {
        TypeMap.Instance.AddType<TestEvent>("TestEvent");
        await _storeFixture.InitializeAsync();
    }

    public async Task DisposeAsync() {
        await _storeFixture.DisposeAsync();
    }

    class ArchiveStore(IEventStore original) : IEventReader, IEventWriter {
        public Task<StreamEvent[]> ReadEvents(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken) {
            return original.ReadEvents(GetArchiveStreamName(stream), start, count, cancellationToken);
        }

        public Task<StreamEvent[]> ReadEventsBackwards(StreamName stream, int count, CancellationToken cancellationToken) {
            return original.ReadEventsBackwards(GetArchiveStreamName(stream), count, cancellationToken);
        }

        static StreamName GetArchiveStreamName(string streamName) => new($"Archive-{streamName}");

        public Task<AppendEventsResult> AppendEvents(
                StreamName                       stream,
                ExpectedStreamVersion            expectedVersion,
                IReadOnlyCollection<StreamEvent> events,
                CancellationToken                cancellationToken
            ) {
            return original.AppendEvents(GetArchiveStreamName(stream), expectedVersion, events, cancellationToken);
        }
    }

    [EventType(TypeName)]
    [UsedImplicitly]
    record TestEvent(string Data, int Number) {
        const string TypeName = "test-event";
    }
}
