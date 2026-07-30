using Eventuous.Sqlite.Subscriptions;
using Eventuous.Sut.App;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;

// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.Sqlite.Subscriptions;

[NotInParallel]
public class SubscribeToAll() : SubscriptionTestBase(Fixture) {
    static readonly SubscriptionFixture<SqliteAllStreamSubscription, SqliteAllStreamSubscriptionOptions, TestEventHandler> Fixture
        = new(_ => { }, false);

    [Test]
    public async Task Sqlite_ShouldConsumeProducedEvents(CancellationToken cancellationToken) {
        const int count = 10;

        var commands   = await GenerateAndHandleCommands(count);
        var testEvents = commands.Select(ToEvent).ToList();

        await Fixture.StartSubscription();
        await Fixture.Handler.AssertCollection(TimeSpan.FromSeconds(5), [..testEvents]).Validate(cancellationToken);
        await Fixture.StopSubscription();
        await Assert.That(Fixture.Handler.Count).IsEqualTo(10);
    }

    [Test]
    public async Task Sqlite_ShouldUseExistingCheckpoint(CancellationToken cancellationToken) {
        const int count = 10;

        await GenerateAndHandleCommands(count);

        await Fixture.CheckpointStore.GetLastCheckpoint(Fixture.SubscriptionId, cancellationToken);
        var last = await Fixture.GetLastPosition();
        await Fixture.CheckpointStore.StoreCheckpoint(new(Fixture.SubscriptionId, last), true, cancellationToken);

        await Fixture.StartSubscription();
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        await Fixture.StopSubscription();
        await Assert.That(Fixture.Handler.Count).IsEqualTo(0);
    }

    static BookingImported ToEvent(ImportBooking cmd) => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    static async Task<List<ImportBooking>> GenerateAndHandleCommands(int count) {
        var commands = Enumerable
            .Range(0, count)
            .Select(_ => DomainFixture.CreateImportBooking())
            .ToList();

        var service = new BookingService(Fixture.EventStore);

        foreach (var cmd in commands) {
            var result = await service.Handle(cmd, default);
            result.ThrowIfError();
        }

        return commands;
    }
}

[NotInParallel]
public class SubscribeToStream() : SubscriptionTestBase(Fixture) {
    static readonly StreamName StreamName = new(Guid.NewGuid().ToString());

    static readonly SubscriptionFixture<SqliteStreamSubscription, SqliteStreamSubscriptionOptions, TestEventHandler> Fixture
        = new(opt => opt.Stream = StreamName, false);

    [Test]
    public async Task Sqlite_ShouldConsumeProducedEvents(CancellationToken cancellationToken) {
        const int count = 10;

        var testEvents = await GenerateAndProduceEvents(count);

        await Fixture.StartSubscription();
        await Fixture.Handler.AssertCollection(TimeSpan.FromSeconds(5), [..testEvents]).Validate(cancellationToken);
        await Fixture.StopSubscription();
        await Assert.That(Fixture.Handler.Count).IsEqualTo(10);
    }

    [Test]
    public async Task Sqlite_ShouldUseExistingCheckpoint(CancellationToken cancellationToken) {
        const int count = 10;

        await GenerateAndProduceEvents(count);

        await Fixture.CheckpointStore.GetLastCheckpoint(Fixture.SubscriptionId, cancellationToken);
        await Fixture.CheckpointStore.StoreCheckpoint(new(Fixture.SubscriptionId, 9), true, cancellationToken);

        await Fixture.StartSubscription();
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        await Fixture.StopSubscription();
        await Assert.That(Fixture.Handler.Count).IsEqualTo(0);
    }

    static BookingImported ToEvent(ImportBooking cmd) => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    static async Task<List<BookingImported>> GenerateAndProduceEvents(int count) {
        var commands = Enumerable
            .Range(0, count)
            .Select(_ => DomainFixture.CreateImportBooking())
            .ToList();

        var events       = commands.Select(ToEvent).ToList();
        var streamEvents = events.Select(x => new NewStreamEvent(Guid.NewGuid(), x, new()));
        await Fixture.EventStore.AppendEvents(StreamName, ExpectedStreamVersion.Any, streamEvents.ToArray(), default);

        return events;
    }
}
