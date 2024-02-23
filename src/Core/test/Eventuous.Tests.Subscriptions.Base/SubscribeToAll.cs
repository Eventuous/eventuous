using DotNet.Testcontainers.Containers;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Persistence.Base.Fixtures;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Subscriptions.Base;

public abstract class SubscribeToAllBase<TContainer, TSubscription, TSubscriptionOptions, TCheckpointStore>(
        ITestOutputHelper outputHelper,
        SubscriptionFixtureBase<TContainer, TSubscription, TSubscriptionOptions, TCheckpointStore, TestEventHandler> fixture
    ) : IAsyncLifetime
    where TContainer : DockerContainer
    where TSubscription : EventSubscription<TSubscriptionOptions>
    where TSubscriptionOptions : SubscriptionOptions
    where TCheckpointStore : class, ICheckpointStore {
    protected async Task ShouldConsumeProducedEvents() {
        const int count = 10;

        var commands   = await GenerateAndHandleCommands(count);
        var testEvents = commands.Select(ToEvent).ToList();

        await fixture.Start();
        await fixture.Handler.AssertCollection(2.Seconds(), [..testEvents]).Validate();
        await fixture.Stop();
        fixture.Handler.Count.Should().Be(10);
    }

    protected async Task ShouldConsumeProducedEventsWhenRestarting() {
        await TestConsumptionOfProducedEvents();

        fixture.Handler.Reset();
        await fixture.InitializeAsync();

        await TestConsumptionOfProducedEvents();

        return;

        async Task TestConsumptionOfProducedEvents() {
            const int count      = 10;
            var       commands   = await GenerateAndHandleCommands(count);
            var       testEvents = commands.Select(ToEvent).ToList();
            await fixture.Start();
            await fixture.Handler.AssertCollection(2.Seconds(), [..testEvents]).Validate();
            await fixture.Stop();
            fixture.Handler.Count.Should().Be(10);
        }
    }

    protected async Task ShouldUseExistingCheckpoint() {
        const int count = 10;

        await GenerateAndHandleCommands(count);

        await fixture.CheckpointStore.GetLastCheckpoint(fixture.SubscriptionId, default);
        var last = await fixture.GetLastPosition();
        await fixture.CheckpointStore.StoreCheckpoint(new Checkpoint(fixture.SubscriptionId, last), true, default);
        
        var l = await fixture.CheckpointStore.GetLastCheckpoint(fixture.SubscriptionId, default);
        outputHelper.WriteLine("Last checkpoint: {0}", l.Position);

        await fixture.Start();
        await Task.Delay(TimeSpan.FromSeconds(1));
        await fixture.Stop();
        fixture.Handler.Count.Should().Be(0);
    }

    static BookingImported ToEvent(ImportBooking cmd) => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    async Task<List<ImportBooking>> GenerateAndHandleCommands(int count) {
        var commands = Enumerable
            .Range(0, count)
            .Select(_ => DomainFixture.CreateImportBooking(fixture.Auto))
            .ToList();

        var service = new BookingService(fixture.AggregateStore);

        foreach (var cmd in commands) {
            var result = await service.Handle(cmd, default);

            if (result is ErrorResult<BookingState> error) {
                throw error.Exception ?? new Exception(error.Message);
            }
        }

        return commands;
    }

    public Task InitializeAsync() => fixture.InitializeAsync();

    public Task DisposeAsync() => fixture.DisposeAsync();
}
