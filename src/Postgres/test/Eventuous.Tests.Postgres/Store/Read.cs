using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Postgres.Fixtures;

namespace Eventuous.Tests.Postgres.Store;

public class Read(IntegrationFixture fixture) : IClassFixture<IntegrationFixture> {
    [Fact]
    public async Task ShouldReadOne() {
        var evt        = fixture.CreateEvent();
        var streamName = fixture.GetStreamName();
        await fixture.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream);

        var result = await fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 100, default);
        result.Length.Should().Be(1);
        result[0].Payload.Should().BeEquivalentTo(evt);
    }

    [Fact]
    public async Task ShouldReadMany() {
        // ReSharper disable once CoVariantArrayConversion
        object[] events     = fixture.CreateEvents(20).ToArray();
        var      streamName = fixture.GetStreamName();
        await fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result = await fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 100, default);
        var actual = result.Select(x => x.Payload);
        actual.Should().BeEquivalentTo(events);
    }

    [Fact]
    public async Task ShouldReadTail() {
        // ReSharper disable once CoVariantArrayConversion
        object[] events     = fixture.CreateEvents(20).ToArray();
        var      streamName = fixture.GetStreamName();
        await fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result   = await fixture.EventStore.ReadEvents(streamName, new StreamReadPosition(10), 100, default);
        var expected = events.Skip(10);
        var actual   = result.Select(x => x.Payload);
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task ShouldReadHead() {
        // ReSharper disable once CoVariantArrayConversion
        object[] events     = fixture.CreateEvents(20).ToArray();
        var      streamName = fixture.GetStreamName();
        await fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result   = await fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 10, default);
        var expected = events.Take(10);
        var actual   = result.Select(x => x.Payload);
        actual.Should().BeEquivalentTo(expected);
    }
}
