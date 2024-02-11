using Eventuous.Tests.Persistence.Base.Fixtures;

namespace Eventuous.Tests.Persistence.Base.Store;

public abstract class StoreOtherOpsTests<T>(T fixture) : IClassFixture<T> where T : StoreFixtureBase {
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
