using System.Globalization;
using Eventuous.Tests.Redis.Fixtures;
using Shouldly;
using static Eventuous.Tests.Redis.Store.Helpers;

namespace Eventuous.Tests.Redis.Store;

[ClassDataSource<IntegrationFixture>]
public class ReadEvents(IntegrationFixture fixture) {
    [Test]
    public async Task ShouldReadOne(CancellationToken cancellationToken) {
        var evt        = CreateEvent();
        var streamName = GetStreamName();
        await fixture.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream, cancellationToken);

        var result = await fixture.EventReader.ReadEvents(streamName, StreamReadPosition.Start, 100, true, cancellationToken);

        result.Length.ShouldBe(1);
        result[0].Payload.ShouldBeEquivalentTo(evt);
    }

    [Test]
    public async Task ShouldReadMany(CancellationToken cancellationToken) {
        // ReSharper disable once CoVariantArrayConversion
        var events     = CreateEvents(20).ToArray();
        var streamName = GetStreamName();
        await fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream, cancellationToken);

        var result = await fixture.EventReader.ReadEvents(streamName, StreamReadPosition.Start, 100, true, cancellationToken);

        IEnumerable<object> actual = result.Select(x => x.Payload)!;
        await Assert.That(actual).IsEquivalentTo(events);
    }

    [Test]
    public async Task ShouldReadTail(CancellationToken cancellationToken) {
        // ReSharper disable once CoVariantArrayConversion
        var streamName = GetStreamName();

        var events1  = CreateEvents(10).ToArray();
        var appended = await fixture.AppendEvents(streamName, events1, ExpectedStreamVersion.NoStream, cancellationToken);
        var position = appended.GlobalPosition;

        var events2 = CreateEvents(10).ToArray();
        await fixture.AppendEvents(streamName, events2, ExpectedStreamVersion.Any, cancellationToken);

        // The read position is inclusive, so start from the position right after the first batch
        var result = await fixture.EventReader.ReadEvents(streamName, new((long)position + 1), 100, true, cancellationToken);

        IEnumerable<object> actual = result.Select(x => x.Payload)!;
        await Assert.That(actual).IsEquivalentTo(events2);
    }

    [Test]
    public async Task ShouldReadStreamToEndAcrossPages(CancellationToken cancellationToken) {
        // A single batch this large lands in one millisecond, so all positions must still round-trip
        var events     = CreateEvents(25).ToArray();
        var streamName = GetStreamName();
        await fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream, cancellationToken);

        var result = new List<StreamEvent>();

        await foreach (var evt in fixture.EventReader.ReadStreamToEnd(streamName, StreamReadPosition.Start, pageSize: 4, cancellationToken: cancellationToken)) {
            result.Add(evt);
        }

        IEnumerable<object> actual = result.Select(x => x.Payload)!;
        await Assert.That(actual).IsEquivalentTo(events);
    }

    [Test]
    public async Task ShouldRejectLegacyUnrepresentableEntryId(CancellationToken cancellationToken) {
        var streamName = GetStreamName();

        // Entries written by older versions can carry auto-generated IDs with sequence numbers
        // the position encoding can't represent; reading them must fail loudly, not garble positions
        await AddLegacyEntry(streamName, "12345-10");

        await Assert.ThrowsAsync<NotSupportedException>(() => fixture.EventReader.ReadEvents(streamName, StreamReadPosition.Start, 10, true, cancellationToken));
    }

    [Test]
    public async Task ShouldRejectLegacyEntryIdHiddenBehindPageBoundary(CancellationToken cancellationToken) {
        var streamName = GetStreamName();

        // Legacy auto-generated IDs from a same-millisecond burst
        for (var sequence = 0; sequence <= 10; sequence++) {
            await AddLegacyEntry(streamName, $"12345-{sequence}");
        }

        // The first page ends at 12345-9 and the advanced position decodes past 12345-10,
        // which must fail loudly instead of being silently skipped
        await Assert.ThrowsAsync<NotSupportedException>(ReadFunc);

        return;

        async Task ReadFunc() {
            await foreach (var _ in fixture.EventReader.ReadStreamToEnd(streamName, StreamReadPosition.Start, pageSize: 10, cancellationToken: cancellationToken)) { }
        }
    }

    async Task AddLegacyEntry(StreamName streamName, string id) {
        var serialized = EventSerializer.Default.SerializeEvent(CreateEvent());

        await fixture.GetDatabase().StreamAddAsync(
            streamName.ToString(),
            [
                new("message_id", Guid.NewGuid().ToString()),
                new("message_type", serialized.EventType),
                new("json_data", serialized.Payload),
                new("created", DateTime.UtcNow.ToString(CultureInfo.InvariantCulture))
            ],
            id
        );
    }

    [Test]
    public async Task ShouldReturnEmptyReadingPastEnd(CancellationToken cancellationToken) {
        var events     = CreateEvents(10).ToArray();
        var streamName = GetStreamName();
        var appended   = await fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream, cancellationToken);

        var result = await fixture.EventReader.ReadEvents(streamName, new((long)appended.GlobalPosition + 1000), 10, true, cancellationToken);

        await Assert.That(result).IsEmpty();
    }

    [Test]
    public async Task ShouldThrowWhenReadingMissingStream(CancellationToken cancellationToken) {
        var streamName = GetStreamName();

        await Assert.ThrowsAsync<StreamNotFound>(() => fixture.EventReader.ReadEvents(streamName, StreamReadPosition.Start, 10, true, cancellationToken));
    }

    [Test]
    public async Task ShouldReadHead(CancellationToken cancellationToken) {
        // ReSharper disable once CoVariantArrayConversion
        var events     = CreateEvents(20).ToArray();
        var streamName = GetStreamName();
        await fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream, cancellationToken);

        var result = await fixture.EventReader.ReadEvents(streamName, StreamReadPosition.Start, 10, true, cancellationToken);

        var expected = events.Take(10);
        var actual   = result.Select(x => x.Payload!);
        await Assert.That(actual).IsEquivalentTo(expected);
    }
}
