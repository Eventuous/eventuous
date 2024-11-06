using Eventuous.Tests.Redis.Fixtures;
using static Eventuous.Tests.Redis.Store.Helpers;

namespace Eventuous.Tests.Redis.Store;

[ClassDataSource<IntegrationFixture>]
public class ReadEvents(IntegrationFixture fixture) {
    [Test]
    public async Task ShouldReadOne(CancellationToken cancellationToken) {
        var evt        = CreateEvent();
        var streamName = GetStreamName();
        await fixture.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream, cancellationToken);

        var result = await fixture.EventReader.ReadEvents(streamName, StreamReadPosition.Start, 100, cancellationToken);

        result.Length.Should().Be(1);
        result[0].Payload.Should().BeEquivalentTo(evt);
    }

    [Test]
    public async Task ShouldReadMany(CancellationToken cancellationToken) {
        // ReSharper disable once CoVariantArrayConversion
        var events     = CreateEvents(20).ToArray();
        var streamName = GetStreamName();
        await fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream, cancellationToken);

        var result = await fixture.EventReader.ReadEvents(streamName, StreamReadPosition.Start, 100, cancellationToken);

        var actual = result.Select(x => x.Payload);
        actual.Should().BeEquivalentTo(events);
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

        var result = await fixture.EventReader.ReadEvents(streamName, new((long)position), 100, cancellationToken);

        var actual = result.Select(x => x.Payload);
        actual.Should().BeEquivalentTo(events2);
    }

    [Test]
    public async Task ShouldReadHead(CancellationToken cancellationToken) {
        // ReSharper disable once CoVariantArrayConversion
        var events     = CreateEvents(20).ToArray();
        var streamName = GetStreamName();
        await fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream, cancellationToken);

        var result = await fixture.EventReader.ReadEvents(streamName, StreamReadPosition.Start, 10, cancellationToken);

        var expected = events.Take(10);
        var actual   = result.Select(x => x.Payload);
        actual.Should().BeEquivalentTo(expected);
    }
}
