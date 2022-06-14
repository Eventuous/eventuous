using static Eventuous.Tests.Postgres.Fixtures.IntegrationFixture;
using static Eventuous.Tests.Postgres.Store.Helpers;

namespace Eventuous.Tests.Postgres.Store;

public class Read {
    [Fact]
    public async Task ShouldReadOne() {
        var evt        = CreateEvent();
        var streamName = GetStreamName();
        await AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream);

        var result = await Instance.EventStore.ReadEvents(
            streamName,
            StreamReadPosition.Start,
            100,
            default
        );

        result.Length.Should().Be(1);
        result[0].Payload.Should().BeEquivalentTo(evt);
    }

    [Fact]
    public async Task ShouldReadMany() {
        // ReSharper disable once CoVariantArrayConversion
        object[] events = CreateEvents(20).ToArray();
        var streamName = GetStreamName();
        await AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);
        
        var result = await Instance.EventStore.ReadEvents(
            streamName,
            StreamReadPosition.Start,
            100,
            default
        );

        var actual = result.Select(x => x.Payload);
        actual.Should().BeEquivalentTo(events);
    }
    
    [Fact]
    public async Task ShouldReadTail() {
        // ReSharper disable once CoVariantArrayConversion
        object[] events = CreateEvents(20).ToArray();
        var streamName = GetStreamName();
        await AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);
        
        var result = await Instance.EventStore.ReadEvents(
            streamName,
            new StreamReadPosition(10),
            100,
            default
        );

        var expected = events.Skip(10);
        var actual   = result.Select(x => x.Payload);
        actual.Should().BeEquivalentTo(expected);
    }
    
    [Fact]
    public async Task ShouldReadHead() {
        // ReSharper disable once CoVariantArrayConversion
        object[] events = CreateEvents(20).ToArray();
        var streamName = GetStreamName();
        await AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);
        
        var result = await Instance.EventStore.ReadEvents(
            streamName,
            StreamReadPosition.Start, 
            10,
            default
        );

        var expected = events.Take(10);
        var actual   = result.Select(x => x.Payload);
        actual.Should().BeEquivalentTo(expected);
    }
}
