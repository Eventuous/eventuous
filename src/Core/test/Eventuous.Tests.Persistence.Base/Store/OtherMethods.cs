using Eventuous.Sut.Domain;
using Eventuous.Tests.Persistence.Base.Fixtures;

namespace Eventuous.Tests.Persistence.Base.Store;

public abstract class StoreOtherOpsTests<T> where T : StoreFixtureBase {
    readonly T _fixture;

    protected StoreOtherOpsTests(T fixture) {
        _fixture = fixture;
        fixture.TypeMapper.RegisterKnownEventTypes(typeof(BookingEvents.BookingImported).Assembly);
    }

    [Test]
    [Category("Store")]
    public async Task StreamShouldExist(CancellationToken cancellationToken) {
        var evt        = Helpers.CreateEvent();
        var streamName = Helpers.GetStreamName();
        await _fixture.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream);

        var exists = await _fixture.EventStore.StreamExists(streamName, cancellationToken);
        await Assert.That(exists).IsTrue();
    }

    [Test]
    [Category("Store")]
    public async Task StreamShouldNotExist(CancellationToken cancellationToken) {
        var streamName = Helpers.GetStreamName();
        var exists     = await _fixture.EventStore.StreamExists(streamName, cancellationToken);
        await Assert.That(exists).IsFalse();
    }
}
