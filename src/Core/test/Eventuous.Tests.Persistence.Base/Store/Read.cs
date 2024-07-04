using System.Text.Json;
using Eventuous.Tests.Persistence.Base.Fixtures;

// ReSharper disable CoVariantArrayConversion

namespace Eventuous.Tests.Persistence.Base.Store;

public abstract class StoreReadTests<T>(T fixture) : IClassFixture<T> where T : StoreFixtureBase {
    [Fact]
    [Trait("Category", "Store")]
    public async Task ShouldReadOne() {
        var evt        = fixture.CreateEvent();
        var streamName = fixture.GetStreamName();
        await fixture.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream);

        var result = await fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 100, default);
        result.Length.Should().Be(1);
        result[0].Payload.Should().BeEquivalentTo(evt);
    }

    [Fact]
    [Trait("Category", "Store")]
    public async Task ShouldReadMany() {
        object[] events     = fixture.CreateEvents(20).ToArray();
        var      streamName = fixture.GetStreamName();
        await fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result = await fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 100, default);
        var actual = result.Select(x => x.Payload);
        actual.Should().BeEquivalentTo(events);
    }

    [Fact]
    [Trait("Category", "Store")]
    public async Task ShouldReadTail() {
        object[] events     = fixture.CreateEvents(20).ToArray();
        var      streamName = fixture.GetStreamName();
        await fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result   = await fixture.EventStore.ReadEvents(streamName, new(10), 100, default);
        var expected = events.Skip(10);
        var actual   = result.Select(x => x.Payload);
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    [Trait("Category", "Store")]
    public async Task ShouldReadHead() {
        object[] events     = fixture.CreateEvents(20).ToArray();
        var      streamName = fixture.GetStreamName();
        await fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);

        var result   = await fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 10, default);
        var expected = events.Take(10);
        var actual   = result.Select(x => x.Payload);
        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    [Trait("Category", "Store")]
    public async Task ShouldReadMetadata() {
        var evt        = fixture.CreateEvent();
        var streamName = fixture.GetStreamName();

        await fixture.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream, new() { { "Key1", "Value1" }, { "Key2", "Value2" } });

        var result = await fixture.EventStore.ReadEvents(streamName, StreamReadPosition.Start, 100, default);

        result.Length.Should().Be(1);
        result[0].Payload.Should().BeEquivalentTo(evt);

        result[0]
            .Metadata.ToDictionary(m => m.Key, m => ((JsonElement)m.Value!).GetString())
            .Should()
            .Contain([new("Key1", "Value1"), new("Key2", "Value2")]);
    }
}
