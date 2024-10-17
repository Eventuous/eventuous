using Eventuous.Sut.Domain;
using Eventuous.Tests.Persistence.Base.Fixtures;
using static Xunit.TestContext;

namespace Eventuous.Tests.Persistence.Base.Store;

public abstract class StoreOtherOpsTests<T> : IClassFixture<T> where T : StoreFixtureBase {
    readonly T _fixture;

    protected StoreOtherOpsTests(T fixture) {
        _fixture = fixture;
        fixture.TypeMapper.RegisterKnownEventTypes(typeof(BookingEvents.BookingImported).Assembly);
    }

    [Fact]
    [Trait("Category", "Store")]
    public async Task StreamShouldExist() {
        var evt        = _fixture.CreateEvent();
        var streamName = _fixture.GetStreamName();
        await _fixture.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream);

        var exists = await _fixture.EventStore.StreamExists(streamName, Current.CancellationToken);
        exists.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Store")]
    public async Task StreamShouldNotExist() {
        var streamName = _fixture.GetStreamName();
        var exists     = await _fixture.EventStore.StreamExists(streamName, Current.CancellationToken);
        exists.Should().BeFalse();
    }
}
