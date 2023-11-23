using System.Text.Json;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.SqlServer.Fixtures;

// ReSharper disable CoVariantArrayConversion

namespace Eventuous.Tests.SqlServer.Store;

public class Read(IntegrationFixture fixture) : IClassFixture<IntegrationFixture> {
    [Fact]
    public async Task ShouldReadOne() {
        var evt        = fixture.CreateEvent();
        var streamName = fixture.GetStreamName();
        await fixture.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream);

        var result = await fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 100, default);

        result.Length.Should().Be(1);
        result[0].Payload.Should().BeEquivalentTo(evt);
        result[0].Metadata.Should().BeEquivalentTo(new Metadata());
    }

    [Fact]
    public async Task ShouldReadMetadata() {
        var evt        = fixture.CreateEvent();
        var streamName = fixture.GetStreamName();

        await fixture.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream, new Metadata { { "Key1", "Value1" }, { "Key2", "Value2" } });

        var result = await fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 100, default);

        result.Length.Should().Be(1);
        result[0].Payload.Should().BeEquivalentTo(evt);

        result[0]
            .Metadata.ToDictionary(m => m.Key, m => ((JsonElement)m.Value!).GetString())
            .Should()
            .BeEquivalentTo(new Metadata { { "Key1", "Value1" }, { "Key2", "Value2" } });
    }

    [Fact]
    public async Task ShouldReadMany() {
        object[] events     = fixture.CreateEvents(20).ToArray();
        var      streamName = fixture.GetStreamName();
        await fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result = await fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 100, default);

        var actual = result.Select(x => x.Payload);
        actual.Should().BeEquivalentTo(events);
    }

    [Fact]
    public async Task ShouldReadTail() {
        object[] events     = fixture.CreateEvents(20).ToArray();
        var      streamName = fixture.GetStreamName();
        await fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result = await fixture.EventStore.ReadEvents(streamName, new StreamReadPosition(10), 100, default);

        var expected = events.Skip(10);
        var actual   = result.Select(x => x.Payload);
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task ShouldReadHead() {
        object[] events     = fixture.CreateEvents(20).ToArray();
        var      streamName = fixture.GetStreamName();
        await fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result = await fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 10, default);

        var expected = events.Take(10);
        var actual   = result.Select(x => x.Payload);
        actual.Should().BeEquivalentTo(expected);
    }
}
