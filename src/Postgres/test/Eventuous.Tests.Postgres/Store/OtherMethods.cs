using Eventuous.Tests.Postgres.Fixtures;
using static Eventuous.Tests.Postgres.Store.Helpers;

namespace Eventuous.Tests.Postgres.Store;

public class OtherMethods(IntegrationFixture fixture) : IClassFixture<IntegrationFixture> {
    [Fact]
    public async Task StreamShouldExist() {
        var evt        = CreateEvent();
        var streamName = GetStreamName();
        await fixture.EventStore.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream);

        var exists = await fixture.EventStore.StreamExists(streamName, default);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task StreamShouldNotExist() {
        var streamName = GetStreamName();
        var exists     = await fixture.EventStore.StreamExists(streamName, default);
        exists.Should().BeFalse();
    }
}
