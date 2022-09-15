using static Eventuous.Tests.SqlServer.Fixtures.IntegrationFixture;
using static Eventuous.Tests.SqlServer.Store.Helpers;

namespace Eventuous.Tests.SqlServer.Store;

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
