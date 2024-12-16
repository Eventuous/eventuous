using Eventuous.Sut.Domain;
using Eventuous.Tests.Persistence.Base.Fixtures;

namespace Eventuous.Tests.Persistence.Base.Store;

public abstract class StoreAppendTests<T> where T : StoreFixtureBase {
    readonly T _fixture;

    protected StoreAppendTests(T fixture) {
        fixture.TypeMapper.RegisterKnownEventTypes(typeof(BookingEvents.BookingImported).Assembly);
        _fixture = fixture;
    }

    [Test]
    [Category("Store")]
    public async Task ShouldAppendToNoStream() {
        var evt        = Helpers.CreateEvent();
        var streamName = Helpers.GetStreamName();
        var result     = await _fixture.AppendEvent(streamName, evt, ExpectedStreamVersion.NoStream);

        await Assert.That(result.NextExpectedVersion).IsEqualTo(0);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldAppendOneByOne() {
        var evt    = Helpers.CreateEvent();
        var stream = Helpers.GetStreamName();

        var result = await _fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);

        evt = Helpers.CreateEvent();

        var version = new ExpectedStreamVersion(result.NextExpectedVersion);
        result = await _fixture.AppendEvent(stream, evt, version);

        await Assert.That(result.NextExpectedVersion).IsEqualTo(1);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldFailOnWrongVersionNoStream() {
        var evt    = Helpers.CreateEvent();
        var stream = Helpers.GetStreamName();

        await _fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);

        evt = Helpers.CreateEvent();

        await Assert.That(() => _fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream)).Throws<AppendToStreamException>();
    }

    [Test]
    [Category("Store")]
    public async Task ShouldFailOnWrongVersion() {
        var evt    = Helpers.CreateEvent();
        var stream = Helpers.GetStreamName();

        await _fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);

        evt = Helpers.CreateEvent();

        await Assert.That(() => _fixture.AppendEvent(stream, evt, new(3))).Throws<AppendToStreamException>();
    }
    

    [Test]
    [Category("Store")]
    public async Task ShouldFailOnWrongVersionWithOptimisticConcurrencyException() {
        var evt    = Helpers.CreateEvent();
        var stream = Helpers.GetStreamName();

        await _fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);

        evt = Helpers.CreateEvent();

        await Assert.That(() => _fixture.StoreChanges(stream, evt, new(3))).Throws<OptimisticConcurrencyException>();
    }
}
