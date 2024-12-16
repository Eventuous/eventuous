using DotNet.Testcontainers.Containers;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Sut.App;
using Eventuous.Tests.Persistence.Base.Fixtures;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Subscriptions.Base;

public abstract class SubscribeToAllBase<TContainer, TSubscription, TSubscriptionOptions, TCheckpointStore>(
        SubscriptionFixtureBase<TContainer, TSubscription, TSubscriptionOptions, TCheckpointStore, TestEventHandler> fixture
    ) : SubscriptionTestBase(fixture)
    where TContainer : DockerContainer
    where TSubscription : EventSubscription<TSubscriptionOptions>
    where TSubscriptionOptions : SubscriptionOptions
    where TCheckpointStore : class, ICheckpointStore {
    protected async Task ShouldConsumeProducedEvents(CancellationToken cancellationToken) {
        const int count = 10;

        var commands   = await GenerateAndHandleCommands(count);
        var testEvents = commands.Select(ToEvent).ToList();

        await fixture.StartSubscription();
        await fixture.Handler.AssertCollection(TimeSpan.FromSeconds(2), [..testEvents]).Validate(cancellationToken);
        await fixture.StopSubscription();
        await Assert.That(fixture.Handler.Count).IsEqualTo(10);
    }

    protected async Task ShouldConsumeProducedEventsWhenRestarting(CancellationToken cancellationToken) {
        await TestConsumptionOfProducedEvents();

        fixture.Handler.Reset();
        await fixture.InitializeAsync();

        await TestConsumptionOfProducedEvents();

        return;

        async Task TestConsumptionOfProducedEvents() {
            const int count = 10;

            var commands   = await GenerateAndHandleCommands(count);
            var testEvents = commands.Select(ToEvent).ToList();
            await fixture.StartSubscription();
            await fixture.Handler.AssertCollection(TimeSpan.FromSeconds(2), [..testEvents]).Validate(cancellationToken);
            await fixture.StopSubscription();
            await Assert.That(fixture.Handler.Count).IsEqualTo(10);
        }
    }

    protected async Task ShouldUseExistingCheckpoint(CancellationToken cancellationToken) {
        const int count = 10;

        await GenerateAndHandleCommands(count);

        await fixture.CheckpointStore.GetLastCheckpoint(fixture.SubscriptionId, cancellationToken);
        var last = await fixture.GetLastPosition();
        await fixture.CheckpointStore.StoreCheckpoint(new(fixture.SubscriptionId, last), true, cancellationToken);

        var l = await fixture.CheckpointStore.GetLastCheckpoint(fixture.SubscriptionId, cancellationToken);
        TestContext.Current?.OutputWriter.WriteLine("Last checkpoint: {0}", l.Position!);

        await fixture.StartSubscription();
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        await fixture.StopSubscription();
        await Assert.That(fixture.Handler.Count).IsEqualTo(0);
    }

    static BookingImported ToEvent(ImportBooking cmd) => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    async Task<List<ImportBooking>> GenerateAndHandleCommands(int count) {
        var commands = Enumerable
            .Range(0, count)
            .Select(_ => DomainFixture.CreateImportBooking())
            .ToList();

        var service = new BookingService(fixture.EventStore);

        foreach (var cmd in commands) {
            var result = await service.Handle(cmd, default);
            result.ThrowIfError();
        }

        return commands;
    }
}
