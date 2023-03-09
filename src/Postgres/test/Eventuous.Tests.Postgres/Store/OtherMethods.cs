using Eventuous.Tests.Postgres.Fixtures;
using static Eventuous.Tests.Postgres.Store.Helpers;

namespace Eventuous.Tests.Postgres.Store;

public class OtherMethods {
    IntegrationFixture _fixture = new();

    [Fact]
    public async Task StreamShouldExist() {
        var evt        = CreateEvent();
        var streamName = GetStreamName();
        await _fixture.EventStore.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream);

        var exists = await _fixture.EventStore.StreamExists(streamName, default);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task StreamShouldNotExist() {
        var streamName = GetStreamName();
        var exists     = await _fixture.EventStore.StreamExists(streamName, default);
        exists.Should().BeFalse();
    }
}
