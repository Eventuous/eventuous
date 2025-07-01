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

        var actual = result.Select(x => x.Payload);
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

        var result = await fixture.EventReader.ReadEvents(streamName, new((long)position), 100, true, cancellationToken);

        var actual = result.Select(x => x.Payload);
        await Assert.That(actual).IsEquivalentTo(events2);
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
