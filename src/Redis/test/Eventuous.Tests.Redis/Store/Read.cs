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
        // Keep the batch small so the auto-generated Redis ID sequence numbers stay within
        // the single digit that the position encoding can represent
        var events     = CreateEvents(8).ToArray();
        var streamName = GetStreamName();
        await fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream, cancellationToken);

        var result = new List<StreamEvent>();

        await foreach (var evt in fixture.EventReader.ReadStreamToEnd(streamName, StreamReadPosition.Start, pageSize: 3, cancellationToken: cancellationToken)) {
            result.Add(evt);
        }

        IEnumerable<object> actual = result.Select(x => x.Payload)!;
        await Assert.That(actual).IsEquivalentTo(events);
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
