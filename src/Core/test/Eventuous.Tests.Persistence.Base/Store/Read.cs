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
        var evt        = Helpers.CreateEvent();
        var streamName = Helpers.GetStreamName();
        await _fixture.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream);

        var result = await _fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 100, true, cancellationToken);
        await Assert.That(result.Length).IsEqualTo(1);
        await Assert.That(result[0].Payload).IsEquivalentTo(evt);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReadMany(CancellationToken cancellationToken) {
        object[] events     = [.. _fixture.CreateEvents(20)];
        var      streamName = Helpers.GetStreamName();
        await _fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result = await _fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 100, true, cancellationToken);

        IEnumerable<object> actual = result.Select(x => x.Payload)!;
        await Assert.That(actual).IsEquivalentTo(events);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReadTail(CancellationToken cancellationToken) {
        object[] events     = [.. _fixture.CreateEvents(20)];
        var      streamName = Helpers.GetStreamName();
        await _fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result   = await _fixture.EventStore.ReadEvents(streamName, new(10), 100, true, cancellationToken);
        var expected = events.Skip(10);
        var actual   = result.Select(x => x.Payload!);
        await Assert.That(actual).IsEquivalentTo(expected);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReadHead(CancellationToken cancellationToken) {
        object[] events     = [.. _fixture.CreateEvents(20)];
        var      streamName = Helpers.GetStreamName();
        await _fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result   = await _fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 10, true, cancellationToken);
        var expected = events.Take(10);

        IEnumerable<object> actual = result.Select(x => x.Payload)!;
        await Assert.That(actual).IsEquivalentTo(expected);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReadMetadata(CancellationToken cancellationToken) {
        var evt        = Helpers.CreateEvent();
        var streamName = Helpers.GetStreamName();

        await _fixture.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream, new() { { "Key1", "Value1" }, { "Key2", "Value2" } });

        var result = await _fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 100, true, cancellationToken);

        await Assert.That(result.Length).IsEqualTo(1);
        await Assert.That(result[0].Payload).IsEquivalentTo(evt);

        await Assert.That(result[0].Metadata.ToDictionary(m => m.Key, m => ((JsonElement)m.Value!).GetString()))
            .ContainsKey("Key1")
            .And.ContainsKey("Key2");
    }

    [Test]
    [Category("Store")]
    public async Task ShouldThrowWhenReadingForwardsFromNegativePosition(CancellationToken cancellationToken) {
        object[] events     = [.. _fixture.CreateEvents(10)];
        var      streamName = Helpers.GetStreamName();
        await _fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        await Assert.ThrowsAsync(ReadFunc);

        return;

        // Try to read from negative position
        Task<StreamEvent[]> ReadFunc() => _fixture.EventStore.ReadEvents(streamName, new(-10), 5, true, cancellationToken);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReadBackwardsFromEnd(CancellationToken cancellationToken) {
        object[] events     = [.. _fixture.CreateEvents(10)];
        var      streamName = Helpers.GetStreamName();
        await _fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result = await _fixture.EventStore.ReadEventsBackwards(streamName, new(9), 3, true, cancellationToken);

        await Assert.That(result.Length).IsEqualTo(3);
        // Events should be in reverse order: positions 9, 8, 7
        await Assert.That(result[0].Payload).IsEquivalentTo(events[9]);
        await Assert.That(result[1].Payload).IsEquivalentTo(events[8]);
        await Assert.That(result[2].Payload).IsEquivalentTo(events[7]);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReadBackwardsFromMiddle(CancellationToken cancellationToken) {
        object[] events     = [.. _fixture.CreateEvents(20)];
        var      streamName = Helpers.GetStreamName();
        await _fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result = await _fixture.EventStore.ReadEventsBackwards(streamName, new(10), 5, true, cancellationToken);

        await Assert.That(result.Length).IsEqualTo(5);
        // Events should be in reverse order: positions 10, 9, 8, 7, 6
        await Assert.That(result[0].Payload).IsEquivalentTo(events[10]);
        await Assert.That(result[1].Payload).IsEquivalentTo(events[9]);
        await Assert.That(result[2].Payload).IsEquivalentTo(events[8]);
        await Assert.That(result[3].Payload).IsEquivalentTo(events[7]);
        await Assert.That(result[4].Payload).IsEquivalentTo(events[6]);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReturnWhenReadingBackwards(CancellationToken cancellationToken) {
        object[] events     = [.. _fixture.CreateEvents(10)];
        var      streamName = Helpers.GetStreamName();
        await _fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        // Try to read from position 20 when stream only has events at positions 0-9
        var result = await _fixture.EventStore.ReadEventsBackwards(streamName, new(20), 5, true, cancellationToken);

        await Assert.That(result.Length).IsEqualTo(5);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldThrowWhenReadingMissingStream(CancellationToken cancellationToken) {
        var streamName = Helpers.GetStreamName();

        await Assert.ThrowsAsync<StreamNotFound>(() => _fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 10, true, cancellationToken));
    }

    [Test]
    [Category("Store")]
    public async Task ShouldThrowWhenReadingMissingStreamBackwards(CancellationToken cancellationToken) {
        var streamName = Helpers.GetStreamName();

        await Assert.ThrowsAsync<StreamNotFound>(() => _fixture.EventStore.ReadEventsBackwards(streamName, StreamReadPosition.End, 10, true, cancellationToken));
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReadStreamToEnd(CancellationToken cancellationToken) {
        object[] events     = [.. _fixture.CreateEvents(25)];
        var      streamName = Helpers.GetStreamName();
        await _fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result = new List<StreamEvent>();

        await foreach (var evt in _fixture.EventStore.ReadStreamToEnd(streamName, StreamReadPosition.Start, pageSize: 10, cancellationToken: cancellationToken)) {
            result.Add(evt);
        }

        IEnumerable<object> actual = result.Select(x => x.Payload)!;
        await Assert.That(actual).IsEquivalentTo(events);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReadStreamToEndWithExactPageMultiple(CancellationToken cancellationToken) {
        object[] events     = [.. _fixture.CreateEvents(20)];
        var      streamName = Helpers.GetStreamName();
        await _fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result = new List<StreamEvent>();

        await foreach (var evt in _fixture.EventStore.ReadStreamToEnd(streamName, StreamReadPosition.Start, pageSize: 10, cancellationToken: cancellationToken)) {
            result.Add(evt);
        }

        IEnumerable<object> actual = result.Select(x => x.Payload)!;
        await Assert.That(actual).IsEquivalentTo(events);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReadStreamToEndFromPosition(CancellationToken cancellationToken) {
        object[] events     = [.. _fixture.CreateEvents(25)];
        var      streamName = Helpers.GetStreamName();
        await _fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result = new List<StreamEvent>();

        await foreach (var evt in _fixture.EventStore.ReadStreamToEnd(streamName, new(10), pageSize: 10, cancellationToken: cancellationToken)) {
            result.Add(evt);
        }

        var expected = events.Skip(10);
        var actual   = result.Select(x => x.Payload!);
        await Assert.That(actual).IsEquivalentTo(expected);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldRejectInvalidPageSizeReadingToEnd(CancellationToken cancellationToken) {
        var streamName = Helpers.GetStreamName();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Read(0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Read(-1));

        return;

        async Task Read(int pageSize) {
            await foreach (var _ in _fixture.EventStore.ReadStreamToEnd(streamName, StreamReadPosition.Start, pageSize: pageSize, cancellationToken: cancellationToken)) { }
        }
    }

    [Test]
    [Category("Store")]
    public async Task ShouldThrowWhenReadingMissingStreamToEnd(CancellationToken cancellationToken) {
        var streamName = Helpers.GetStreamName();

        await Assert.ThrowsAsync<StreamNotFound>(ReadFunc);

        return;

        async Task ReadFunc() {
            await foreach (var _ in _fixture.EventStore.ReadStreamToEnd(streamName, StreamReadPosition.Start, cancellationToken: cancellationToken)) { }
        }
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReturnNothingWhenReadingMissingStreamToEnd(CancellationToken cancellationToken) {
        var streamName = Helpers.GetStreamName();

        var result = new List<StreamEvent>();

        await foreach (var evt in _fixture.EventStore.ReadStreamToEnd(streamName, StreamReadPosition.Start, failIfNotFound: false, cancellationToken: cancellationToken)) {
            result.Add(evt);
        }

        await Assert.That(result).IsEmpty();
    }

    [Test]
    [Category("Store")]
    public async Task ShouldThrowWhenReadingBackwardsFromNegativePosition(CancellationToken cancellationToken) {
        object[] events     = [.. _fixture.CreateEvents(10)];
        var      streamName = Helpers.GetStreamName();
        await _fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        await Assert.ThrowsAsync(ReadFunc);

        return;

        // Try to read from negative position
        Task<StreamEvent[]> ReadFunc() => _fixture.EventStore.ReadEventsBackwards(streamName, new(-10), 5, true, cancellationToken);
    }
}
