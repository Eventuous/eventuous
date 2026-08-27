using Eventuous.Sqlite.Subscriptions;
using Eventuous.Sut.App;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using static Eventuous.Sut.Domain.BookingEvents;

// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.Sqlite.Subscriptions;

/// <summary>
/// SQLite counterpart of <see cref="SubscriptionMeasureBase{TContainer,TSubscription,TSubscriptionOptions,TCheckpointStore}"/>,
/// written against the standalone SQLite fixture since it isn't container-backed.
/// </summary>
[NotInParallel]
public class SubscriptionMeasure() : SubscriptionTestBase(Fixture) {
    static readonly SubscriptionFixture<SqliteAllStreamSubscription, SqliteAllStreamSubscriptionOptions, TestEventHandler> Fixture
        = new(_ => { }, false);

    [Test]
    public async Task Sqlite_ShouldMeasureEndOfStream(CancellationToken cancellationToken) {
        var measure = Fixture.GetMeasure();

        // An empty store must yield a valid measure at position zero, not EndOfStream.Invalid.
        var empty = await measure(cancellationToken);
        await Assert.That(empty.SubscriptionId).IsEqualTo(Fixture.SubscriptionId);
        await Assert.That(empty.Position).IsEqualTo(0ul);

        // After appending events, the measure must report the global end position.
        await GenerateAndHandleCommands(10);
        var last = await Fixture.GetLastPosition();

        var measured = await measure(cancellationToken);
        await Assert.That(measured.SubscriptionId).IsEqualTo(Fixture.SubscriptionId);
        await Assert.That(measured.Position).IsEqualTo(last);
    }

    static async Task GenerateAndHandleCommands(int count) {
        var commands = Enumerable
            .Range(0, count)
            .Select(_ => DomainFixture.CreateImportBooking())
            .ToList();

        var service = new BookingService(Fixture.EventStore);

        foreach (var cmd in commands) {
            var result = await service.Handle(cmd, default);
            result.ThrowIfError();
        }
    }
}

/// <summary>
/// For a stream subscription, the measure must report the tail of the subscribed stream only (GitHub #586).
/// </summary>
[NotInParallel]
public class StreamSubscriptionMeasure() : SubscriptionTestBase(Fixture) {
    static readonly StreamName StreamName = new(Guid.NewGuid().ToString());

    static readonly SubscriptionFixture<SqliteStreamSubscription, SqliteStreamSubscriptionOptions, TestEventHandler> Fixture
        = new(opt => opt.Stream = StreamName, false);

    [Test]
    public async Task Sqlite_ShouldMeasureEndOfSubscribedStream(CancellationToken cancellationToken) {
        const int otherStreamLength      = 20;
        const int subscribedStreamLength = 5;

        var measure = Fixture.GetMeasure();

        // Invoked before the subscription has ever connected, as the metrics observer does. The subscribed stream
        // doesn't exist yet, which must read as an empty stream rather than EndOfStream.Invalid.
        var empty = await measure(cancellationToken);
        await Assert.That(empty.SubscriptionId).IsEqualTo(Fixture.SubscriptionId);
        await Assert.That(empty.Position).IsEqualTo(0ul);

        // A longer, unrelated stream: its tail must not leak into the subscribed stream's measure.
        await AppendEvents(new($"other-{Guid.NewGuid():N}"), otherStreamLength);
        var events = await AppendEvents(StreamName, subscribedStreamLength);

        var measured = await measure(cancellationToken);
        await Assert.That(measured.SubscriptionId).IsEqualTo(Fixture.SubscriptionId);
        await Assert.That(measured.Position).IsEqualTo((ulong)(subscribedStreamLength - 1));

        await Fixture.StartSubscription();
        await Fixture.Handler.AssertCollection(TimeSpan.FromSeconds(5), [.. events]).Validate(cancellationToken);
        await Fixture.StopSubscription();

        // Once caught up, the gap the metrics derive from this measure and the checkpoint must be zero.
        var checkpoint = await Fixture.CheckpointStore.GetLastCheckpoint(Fixture.SubscriptionId, cancellationToken);
        var caughtUp   = await measure(cancellationToken);
        await Assert.That(caughtUp.Position - checkpoint.Position!.Value).IsEqualTo(0ul);
    }

    static async Task<List<BookingImported>> AppendEvents(StreamName streamName, int count) {
        var events       = Enumerable.Range(0, count).Select(_ => Helpers.CreateEvent()).ToList();
        var streamEvents = events.Select(x => new NewStreamEvent(Guid.NewGuid(), x, new()));
        await Fixture.EventStore.AppendEvents(streamName, ExpectedStreamVersion.NoStream, [.. streamEvents], default);

        return events;
    }
}
