using Eventuous.Tests.Redis.Fixtures;
using static Eventuous.Tests.Redis.Store.Helpers;

namespace Eventuous.Tests.Redis.Store;

[ClassDataSource<IntegrationFixture>]
public class AppendEvents(IntegrationFixture fixture) {
    [Test]
    public async Task ShouldAppendToNoStream(CancellationToken cancellationToken) {
        var evt        = CreateEvent();
        var streamName = GetStreamName();
        var result     = await fixture.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream, cancellationToken);
        result.NextExpectedVersion.Should().Be(0);
    }

    [Test]
    public async Task ShouldAppendOneByOne(CancellationToken cancellationToken) {
        var evt    = CreateEvent();
        var stream = GetStreamName();

        var result = await fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream, cancellationToken);
        evt = CreateEvent();

        var version = new ExpectedStreamVersion(result.NextExpectedVersion);
        result = await fixture.AppendEvent(stream, evt, version, cancellationToken);
        result.NextExpectedVersion.Should().Be(1);
    }

    [Test]
    public async Task ShouldFailOnWrongVersionNoStream(CancellationToken cancellationToken) {
        var evt    = CreateEvent();
        var stream = GetStreamName();

        await fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream, cancellationToken);

        evt = CreateEvent();

        var task = () => fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream, cancellationToken);
        await task.Should().ThrowAsync<AppendToStreamException>();
    }

    [Test]
    public async Task ShouldFailOnWrongVersion(CancellationToken cancellationToken) {
        var evt    = CreateEvent();
        var stream = GetStreamName();

        await fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream, cancellationToken);

        evt = CreateEvent();

        var task = () => fixture.AppendEvent(stream, evt, new(3), cancellationToken);
        await task.Should().ThrowAsync<AppendToStreamException>();
    }
}
