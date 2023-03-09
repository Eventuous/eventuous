using Eventuous.Tests.Redis.Fixtures;
using static Eventuous.Tests.Redis.Store.Helpers;

namespace Eventuous.Tests.Redis.Store;

public class AppendEvents : IAsyncLifetime {
    readonly IntegrationFixture _fixture = new();

    [Fact]
    public async Task ShouldAppendToNoStream() {
        var evt        = CreateEvent();
        var streamName = GetStreamName();
        var result     = await _fixture.EventStore.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream);

        result.NextExpectedVersion.Should().Be(0);
    }

    [Fact]
    public async Task ShouldAppendOneByOne() {
        var evt    = CreateEvent();
        var stream = GetStreamName();

        var result = await _fixture.EventStore.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);

        evt = CreateEvent();

        var version = new ExpectedStreamVersion(result.NextExpectedVersion);
        result = await _fixture.EventStore.AppendEvent(stream, evt, version);

        result.NextExpectedVersion.Should().Be(1);
    }

    [Fact]
    public async Task ShouldFailOnWrongVersionNoStream() {
        var evt    = CreateEvent();
        var stream = GetStreamName();

        await _fixture.EventStore.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);

        evt = CreateEvent();

        var task = () => _fixture.EventStore.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);
        await task.Should().ThrowAsync<AppendToStreamException>();
    }

    [Fact]
    public async Task ShouldFailOnWrongVersion() {
        var evt    = CreateEvent();
        var stream = GetStreamName();

        await _fixture.EventStore.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);

        evt = CreateEvent();

        var task = () => _fixture.EventStore.AppendEvent(stream, evt, new ExpectedStreamVersion(3));
        await task.Should().ThrowAsync<AppendToStreamException>();
    }

    public Task InitializeAsync()
        => _fixture.Initialize();

    public Task DisposeAsync() {
        _fixture.Dispose();
        return Task.CompletedTask;
    }
}
