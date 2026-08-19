using Eventuous.KurrentDB;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Persistence.Base.Fixtures;
using KurrentDB.Client;

namespace Eventuous.Tests.KurrentDB.Store;

[ClassDataSource<StoreFixture>]
public class StreamingReadTests {
    readonly StoreFixture _fixture;

    public StreamingReadTests(StoreFixture fixture) {
        fixture.TypeMapper.RegisterKnownEventTypes(typeof(BookingEvents.BookingImported).Assembly);
        _fixture = fixture;
    }

    const int EventCount = 100;

    [Test]
    [Category("Store")]
    public async Task ShouldStreamEventsForwardsWithoutBufferingWholeRead(CancellationToken cancellationToken) {
        var serializer = new CountingSerializer(_fixture.Serializer);
        var store      = new KurrentDBEventStore(_fixture.Client, serializer);

        object[] events     = [.. _fixture.CreateEvents(EventCount)];
        var      streamName = Helpers.GetStreamName();
        await _fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var deserializedAtFirstYield = 0;

        await foreach (var _ in store.ReadEvents(streamName, StreamReadPosition.Start, EventCount, cancellationToken)) {
            if (deserializedAtFirstYield == 0) deserializedAtFirstYield = serializer.DeserializedCount;
        }

        await Assert.That(deserializedAtFirstYield).IsEqualTo(1);
        await Assert.That(serializer.DeserializedCount).IsEqualTo(EventCount);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldStreamEventsBackwardsWithoutBufferingWholeRead(CancellationToken cancellationToken) {
        var serializer = new CountingSerializer(_fixture.Serializer);
        var store      = new KurrentDBEventStore(_fixture.Client, serializer);

        object[] events     = [.. _fixture.CreateEvents(EventCount)];
        var      streamName = Helpers.GetStreamName();
        await _fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var deserializedAtFirstYield = 0;

        await foreach (var _ in store.ReadEventsBackwards(streamName, new(EventCount - 1), EventCount, cancellationToken)) {
            if (deserializedAtFirstYield == 0) deserializedAtFirstYield = serializer.DeserializedCount;
        }

        await Assert.That(deserializedAtFirstYield).IsEqualTo(1);
        await Assert.That(serializer.DeserializedCount).IsEqualTo(EventCount);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReadRequestedCountWhenSystemEventsAreSkipped(CancellationToken cancellationToken) {
        var (streamName, events) = await SeedStreamWithSystemEvent(cancellationToken);

        var result = new List<StreamEvent>();

        await foreach (var evt in _fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, events.Length, cancellationToken)) {
            result.Add(evt);
        }

        IEnumerable<object> actual = result.Select(x => x.Payload)!;
        await Assert.That(actual).IsEquivalentTo(events);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReadRequestedCountBackwardsWhenSystemEventsAreSkipped(CancellationToken cancellationToken) {
        var (streamName, events) = await SeedStreamWithSystemEvent(cancellationToken);

        var result = new List<StreamEvent>();

        await foreach (var evt in _fixture.EventStore.ReadEventsBackwards(streamName, StreamReadPosition.End, events.Length, cancellationToken)) {
            result.Add(evt);
        }

        IEnumerable<object> actual = result.Select(x => x.Payload).Reverse()!;
        await Assert.That(actual).IsEquivalentTo(events);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReadStreamToEndWhenSystemEventsAreSkipped(CancellationToken cancellationToken) {
        var (streamName, events) = await SeedStreamWithSystemEvent(cancellationToken);

        var result = new List<StreamEvent>();

        // The system event lands inside the first page, which then yields fewer events than the page size
        await foreach (var evt in _fixture.EventStore.ReadStreamToEnd(streamName, StreamReadPosition.Start, pageSize: 6, cancellationToken: cancellationToken)) {
            result.Add(evt);
        }

        IEnumerable<object> actual = result.Select(x => x.Payload)!;
        await Assert.That(actual).IsEquivalentTo(events);
    }

    // Seeds a stream of 12 events where revision 5 is a non-deserializable $-typed event,
    // which the store skips when reading. Returns the 11 deserializable events.
    async Task<(StreamName Stream, object[] Events)> SeedStreamWithSystemEvent(CancellationToken cancellationToken) {
        var      streamName = Helpers.GetStreamName();
        object[] first      = [.. _fixture.CreateEvents(5)];
        object[] rest       = [.. _fixture.CreateEvents(6)];

        await _fixture.AppendEvents(streamName, first, ExpectedStreamVersion.NoStream);

        await _fixture.Client.AppendToStreamAsync(
            streamName.ToString(),
            StreamState.Any,
            [new EventData(Uuid.NewUuid(), "$test-skipped", "{}"u8.ToArray())],
            cancellationToken: cancellationToken
        );

        await _fixture.AppendEvents(streamName, rest, ExpectedStreamVersion.Any);

        return (streamName, [.. first, .. rest]);
    }

    class CountingSerializer(IEventSerializer inner) : IEventSerializer {
        int _deserializedCount;

        public int DeserializedCount => _deserializedCount;

        public DeserializationResult DeserializeEvent(ReadOnlySpan<byte> data, string eventType, string contentType) {
            Interlocked.Increment(ref _deserializedCount);

            return inner.DeserializeEvent(data, eventType, contentType);
        }

        public SerializationResult SerializeEvent(object evt) => inner.SerializeEvent(evt);
    }
}
