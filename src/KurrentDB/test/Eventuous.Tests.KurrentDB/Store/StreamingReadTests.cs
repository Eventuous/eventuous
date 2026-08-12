using Eventuous.KurrentDB;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Persistence.Base.Fixtures;

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
