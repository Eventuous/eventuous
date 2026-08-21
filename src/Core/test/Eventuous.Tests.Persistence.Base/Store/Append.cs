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

        await Assert.That(() => _fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream)!).Throws<AppendToStreamException>();
    }

    [Test]
    [Category("Store")]
    public async Task ShouldFailOnWrongVersion() {
        var evt    = Helpers.CreateEvent();
        var stream = Helpers.GetStreamName();

        await _fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);

        evt = Helpers.CreateEvent();

        await Assert.That(() => _fixture.AppendEvent(stream, evt, new(3))!).Throws<AppendToStreamException>();
    }

    [Test]
    [Category("Store")]
    public async Task ShouldFailOnWrongVersionWithOptimisticConcurrencyException() {
        var evt    = Helpers.CreateEvent();
        var stream = Helpers.GetStreamName();

        await _fixture.AppendEvent(stream, evt, ExpectedStreamVersion.NoStream);

        evt = Helpers.CreateEvent();

        await Assert.That(() => _fixture.StoreChanges(stream, evt, new(3))!).Throws<OptimisticConcurrencyException>();
    }

    [Test]
    [Category("Store")]
    public async Task ShouldAppendToMultipleStreams() {
        var evt1    = Helpers.CreateEvent();
        var evt2    = Helpers.CreateEvent();
        var stream1 = Helpers.GetStreamName();
        var stream2 = Helpers.GetStreamName();

        var results = await _fixture.AppendEventsToMultipleStreams(
            [
                new(stream1, ExpectedStreamVersion.NoStream, [new(Guid.NewGuid(), evt1, new())]),
                new(stream2, ExpectedStreamVersion.NoStream, [new(Guid.NewGuid(), evt2, new())])
            ]
        );

        await Assert.That(results).HasCount().EqualTo(2);
        await Assert.That(results[0].NextExpectedVersion).IsEqualTo(0);
        await Assert.That(results[1].NextExpectedVersion).IsEqualTo(0);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldAppendToMultipleStreamsWithExistingStreams() {
        var stream1 = Helpers.GetStreamName();
        var stream2 = Helpers.GetStreamName();

        var r1 = await _fixture.AppendEvent(stream1, Helpers.CreateEvent(), ExpectedStreamVersion.NoStream);
        var r2 = await _fixture.AppendEvent(stream2, Helpers.CreateEvent(), ExpectedStreamVersion.NoStream);

        var results = await _fixture.AppendEventsToMultipleStreams(
            [
                new(stream1, new(r1.NextExpectedVersion), [new(Guid.NewGuid(), Helpers.CreateEvent(), new())]),
                new(stream2, new(r2.NextExpectedVersion), [new(Guid.NewGuid(), Helpers.CreateEvent(), new())])
            ]
        );

        await Assert.That(results).HasCount().EqualTo(2);
        await Assert.That(results[0].NextExpectedVersion).IsEqualTo(1);
        await Assert.That(results[1].NextExpectedVersion).IsEqualTo(1);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldReturnEmptyResultsForEmptyAppends() {
        var results = await _fixture.AppendEventsToMultipleStreams([]);
        await Assert.That(results).HasCount().EqualTo(0);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldNotLoseConcurrentAppends(CancellationToken cancellationToken) {
        const int writers = 20;

        var stream = Helpers.GetStreamName();
        await _fixture.AppendEvent(stream, Helpers.CreateEvent(), ExpectedStreamVersion.NoStream);

        var results   = await Task.WhenAll(Enumerable.Range(0, writers).Select(_ => TryAppend(stream, ExpectedStreamVersion.Any)));
        var succeeded = results.Count(x => x);

        var stored = await _fixture.EventStore.ReadEvents(stream, StreamReadPosition.Start, writers * 2, true, cancellationToken);

        await Assert.That(succeeded).IsGreaterThan(0);
        await Assert.That(stored.Length).IsEqualTo(1 + succeeded);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldRejectConcurrentAppendsWithSameVersion(CancellationToken cancellationToken) {
        const int writers = 20;

        var stream = Helpers.GetStreamName();
        await _fixture.AppendEvent(stream, Helpers.CreateEvent(), ExpectedStreamVersion.NoStream);

        var results   = await Task.WhenAll(Enumerable.Range(0, writers).Select(_ => TryAppend(stream, new(0))));
        var succeeded = results.Count(x => x);

        var stored = await _fixture.EventStore.ReadEvents(stream, StreamReadPosition.Start, writers * 2, true, cancellationToken);

        await Assert.That(succeeded).IsEqualTo(1);
        await Assert.That(stored.Length).IsEqualTo(1 + succeeded);
    }

    async Task<bool> TryAppend(StreamName stream, ExpectedStreamVersion version) {
        try {
            await _fixture.AppendEvent(stream, Helpers.CreateEvent(), version);

            return true;
        } catch (AppendToStreamException) {
            return false;
        }
    }
}
