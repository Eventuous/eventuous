using static Eventuous.Tests.Postgres.Fixtures.IntegrationFixture;
using static Eventuous.Tests.Postgres.Store.Helpers;

namespace Eventuous.Tests.Postgres.Store;

public class OtherMethods {
    [Fact]
    public async Task StreamShouldExist() {
        var evt        = CreateEvent();
        var streamName = GetStreamName();
        await AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream);

        var exists = await Instance.EventStore.StreamExists(streamName, default);
        exists.Should().BeTrue();
    }
    
    [Fact]
    public async Task StreamShouldNotExist() {
        var streamName = GetStreamName();
        var exists = await Instance.EventStore.StreamExists(streamName, default);
        exists.Should().BeFalse();
    }
}
