using System.Globalization;
using Eventuous.Tests.Redis.Fixtures;
using Shouldly;
using StackExchange.Redis;
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
        await AddLegacyEntry(fixture.GetDatabase(), streamName, "12345-10");

        await Assert.ThrowsAsync<NotSupportedException>(() => fixture.EventReader.ReadEvents(streamName, StreamReadPosition.Start, 10, true, cancellationToken));
    }

    [Test]
    public async Task ShouldRejectLegacyEntryIdHiddenBehindPageBoundary(CancellationToken cancellationToken) {
        var streamName = GetStreamName();

        // Legacy auto-generated IDs from a same-millisecond burst
        var database = fixture.GetDatabase();

        for (var sequence = 0; sequence <= 10; sequence++) {
            await AddLegacyEntry(database, streamName, $"12345-{sequence}");
        }

        // The first page ends at 12345-9 and the advanced position decodes past 12345-10,
        // which must fail loudly instead of being silently skipped
        await Assert.ThrowsAsync<NotSupportedException>(ReadFunc);

        return;

        async Task ReadFunc() {
            await foreach (var _ in fixture.EventReader.ReadStreamToEnd(streamName, StreamReadPosition.Start, pageSize: 10, cancellationToken: cancellationToken)) { }
        }
    }

    [Test]
    public async Task ShouldRejectEntryIdWithSequenceAboveLongRange(CancellationToken cancellationToken) {
        var streamName = GetStreamName();

        // Redis ID sequence components are unsigned 64-bit; values beyond long range must still
        // surface as the documented NotSupportedException, both when materialized and when validated
        await AddLegacyEntry(fixture.GetDatabase(), streamName, "12345-9223372036854775808");

        await Assert.ThrowsAsync<NotSupportedException>(() => fixture.EventReader.ReadEvents(streamName, StreamReadPosition.Start, 10, true, cancellationToken));
        await Assert.ThrowsAsync<NotSupportedException>(() => fixture.EventReader.ReadEvents(streamName, new(123470), 10, true, cancellationToken));
    }

    [Test]
    public async Task ShouldHandleRevisionBoundaryAtLongMax(CancellationToken cancellationToken) {
        // long.MaxValue / 10 = 922337203685477580, long.MaxValue % 10 = 7: sequence 7 encodes to
        // exactly long.MaxValue, sequence 8 no longer fits and must be rejected, not wrap negative
        var fitting = GetStreamName();
        await AddLegacyEntry(fixture.GetDatabase(), fitting, "922337203685477580-7");

        var result = await fixture.EventReader.ReadEvents(fitting, StreamReadPosition.Start, 10, true, cancellationToken);
        await Assert.That(result[0].Revision).IsEqualTo(long.MaxValue);

        var overflowing = GetStreamName();
        await AddLegacyEntry(fixture.GetDatabase(), overflowing, "922337203685477580-8");

        await Assert.ThrowsAsync<NotSupportedException>(() => fixture.EventReader.ReadEvents(overflowing, StreamReadPosition.Start, 10, true, cancellationToken));
    }

    [Test]
    public async Task ShouldReadStreamToEndAtMaxRevision(CancellationToken cancellationToken) {
        // An event at the maximum representable revision filling an exact page must complete
        // the paged read instead of advancing past the end of the position space
        var streamName = GetStreamName();
        await AddLegacyEntry(fixture.GetDatabase(), streamName, "922337203685477580-7");

        var result = new List<StreamEvent>();

        await foreach (var evt in fixture.EventReader.ReadStreamToEnd(streamName, StreamReadPosition.Start, pageSize: 1, cancellationToken: cancellationToken)) {
            result.Add(evt);
        }

        await Assert.That(result).HasCount().EqualTo(1);
        await Assert.That(result[0].Revision).IsEqualTo(long.MaxValue);
    }

    [Test]
    public async Task ShouldRejectLegacyBurstStreamReadFromStart(CancellationToken cancellationToken) {
        var streamName = GetStreamName();

        // A legacy burst with sequence numbers beyond a single decimal carry: positions minted for
        // such entries by older versions are ambiguous, but any read from the start of the stream
        // must reject the first unrepresentable entry it materializes
        var database = fixture.GetDatabase();

        for (var sequence = 0; sequence <= 20; sequence += 5) {
            await AddLegacyEntry(database, streamName, $"12345-{sequence}");
        }

        await Assert.ThrowsAsync<NotSupportedException>(ReadFunc);

        return;

        async Task ReadFunc() {
            await foreach (var _ in fixture.EventReader.ReadStreamToEnd(streamName, StreamReadPosition.Start, cancellationToken: cancellationToken)) { }
        }
    }

    [Test]
    public async Task ShouldRejectResumedCursorOnLegacyBurstStream(CancellationToken cancellationToken) {
        var streamName = GetStreamName();

        var database = fixture.GetDatabase();

        for (var sequence = 0; sequence <= 20; sequence++) {
            await AddLegacyEntry(database, streamName, $"12345-{sequence}");
        }

        // A cursor minted by a pre-fix reader after consuming 12345-19 (revision 123469 + 1):
        // resuming from it must be rejected, not silently skip the remaining entries
        await Assert.ThrowsAsync<NotSupportedException>(() => fixture.EventReader.ReadEvents(streamName, new(123470), 10, true, cancellationToken));
    }

    [Test]
    public async Task ShouldRejectResumedReadAfterStreamRecreatedWithLegacyEntries(CancellationToken cancellationToken) {
        var events     = CreateEvents(3).ToArray();
        var streamName = GetStreamName();
        await fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream, cancellationToken);

        // A resumed read on the clean stream passes validation
        var appended = await fixture.EventReader.ReadEvents(streamName, new(10), 10, true, cancellationToken);
        await Assert.That(appended.Length).IsGreaterThan(0);

        // Recreate the stream under the same name with legacy entries: the earlier verdict must not stick
        var database = fixture.GetDatabase();
        await database.KeyDeleteAsync(streamName.ToString());

        for (var sequence = 0; sequence <= 20; sequence++) {
            await AddLegacyEntry(database, streamName, $"12345-{sequence}");
        }

        await Assert.ThrowsAsync<NotSupportedException>(() => fixture.EventReader.ReadEvents(streamName, new(123470), 10, true, cancellationToken));
    }

    [Test]
    public async Task ShouldRejectResumedReadAfterStreamRestoredWithSameFirstEntry(CancellationToken cancellationToken) {
        var streamName = GetStreamName();
        var database   = fixture.GetDatabase();

        // A clean stream with explicit IDs, validated by a resumed read
        await AddLegacyEntry(database, streamName, "12345-0");
        await AddLegacyEntry(database, streamName, "12346-0");
        await AddLegacyEntry(database, streamName, "12347-0");

        var appended = await fixture.EventReader.ReadEvents(streamName, new(123460), 10, true, cancellationToken);
        await Assert.That(appended.Length).IsGreaterThan(0);

        // Restore the stream with the same first entry but an unrepresentable entry
        // below the previously validated range: the earlier verdict must not stick
        await database.KeyDeleteAsync(streamName.ToString());
        await AddLegacyEntry(database, streamName, "12345-0");
        await AddLegacyEntry(database, streamName, "12346-10");

        await Assert.ThrowsAsync<NotSupportedException>(() => fixture.EventReader.ReadEvents(streamName, new(123470), 10, true, cancellationToken));
    }

    [Test]
    public async Task ShouldRejectResumedReadAfterMissingStreamGetsLegacyEntries(CancellationToken cancellationToken) {
        var streamName = GetStreamName();

        // A resumed read of a missing stream must not establish a verdict for the name
        await Assert.ThrowsAsync<StreamNotFound>(() => fixture.EventReader.ReadEvents(streamName, new(100), 10, true, cancellationToken));

        var database = fixture.GetDatabase();

        for (var sequence = 0; sequence <= 20; sequence++) {
            await AddLegacyEntry(database, streamName, $"12345-{sequence}");
        }

        await Assert.ThrowsAsync<NotSupportedException>(() => fixture.EventReader.ReadEvents(streamName, new(123470), 10, true, cancellationToken));
    }

    static async Task AddLegacyEntry(IDatabase database, StreamName streamName, string id) {
        var serialized = EventSerializer.Default.SerializeEvent(CreateEvent());

        await database.StreamAddAsync(
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
