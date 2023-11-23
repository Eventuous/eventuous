using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Postgres.Fixtures;

namespace Eventuous.Tests.Postgres.Store;

public class OtherMethods(IntegrationFixture fixture) : IClassFixture<IntegrationFixture> {
    [Fact]
    public async Task StreamShouldExist() {
        var evt        = fixture.CreateEvent();
        var streamName = fixture.GetStreamName();
        await fixture.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream);

        var exists = await fixture.EventStore.StreamExists(streamName, default);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task StreamShouldNotExist() {
        var streamName = fixture.GetStreamName();
        var exists     = await fixture.EventStore.StreamExists(streamName, default);
        exists.Should().BeFalse();
    }
}
