using System.Text.Json;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Persistence.Base.Fixtures;

// ReSharper disable CoVariantArrayConversion

namespace Eventuous.Tests.Persistence.Base.Store;

public abstract class StoreReadTests<T> where T : StoreFixtureBase {
    readonly T _fixture;

    protected StoreReadTests(T fixture) {
        fixture.TypeMapper.RegisterKnownEventTypes(typeof(BookingEvents.BookingImported).Assembly);
        _fixture = fixture;
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReadOne(CancellationToken cancellationToken) {
        var evt        = _fixture.CreateEvent();
        var streamName = _fixture.GetStreamName();
        await _fixture.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream);

        var result = await _fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 100, cancellationToken);
        await Assert.That(result.Length).IsEqualTo(1);
        await Assert.That(result[0].Payload).IsEquivalentTo(evt);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReadMany(CancellationToken cancellationToken) {
        object[] events     = _fixture.CreateEvents(20).ToArray();
        var      streamName = _fixture.GetStreamName();
        await _fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result = await _fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 100, cancellationToken);
        var actual = result.Select(x => x.Payload);
        await Assert.That(actual).IsEquivalentTo(events);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReadTail(CancellationToken cancellationToken) {
        object[] events     = _fixture.CreateEvents(20).ToArray();
        var      streamName = _fixture.GetStreamName();
        await _fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result   = await _fixture.EventStore.ReadEvents(streamName, new(10), 100, cancellationToken);
        var expected = events.Skip(10);
        var actual   = result.Select(x => x.Payload);
        await Assert.That(actual).IsEquivalentTo(expected);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReadHead(CancellationToken cancellationToken) {
        object[] events     = _fixture.CreateEvents(20).ToArray();
        var      streamName = _fixture.GetStreamName();
        await _fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result   = await _fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 10, cancellationToken);
        var expected = events.Take(10);

        IEnumerable<object> actual = result.Select(x => x.Payload)!;
        await Assert.That(actual).IsEquivalentCollectionTo(expected);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReadMetadata(CancellationToken cancellationToken) {
        var evt        = _fixture.CreateEvent();
        var streamName = _fixture.GetStreamName();

        await _fixture.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream, new() { { "Key1", "Value1" }, { "Key2", "Value2" } });

        var result = await _fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 100, cancellationToken);

        await Assert.That(result.Length).IsEqualTo(1);
        await Assert.That(result[0].Payload).IsEquivalentTo(evt);

        await Assert.That(result[0].Metadata.ToDictionary(m => m.Key, m => ((JsonElement)m.Value!).GetString()))
            .ContainsKey("Key1").And.ContainsKey("Key2");
    }
}
