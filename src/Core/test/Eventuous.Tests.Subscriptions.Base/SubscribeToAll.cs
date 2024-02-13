using DotNet.Testcontainers.Containers;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Persistence.Base.Fixtures;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Subscriptions.Base;

public abstract class SubscribeToAllBase<TContainer, TSubscription, TSubscriptionOptions, TCheckpointStore>(ITestOutputHelper outputHelper)
    :  SubscriptionTestBase<TContainer, TSubscription, TSubscriptionOptions, TCheckpointStore, TestEventHandler>(outputHelper, false, LogLevel.Debug)
    where TContainer : DockerContainer
    where TSubscription : EventSubscription<TSubscriptionOptions>
    where TSubscriptionOptions : SubscriptionOptions
    where TCheckpointStore : class, ICheckpointStore {
    [Fact]
    public async Task ShouldConsumeProducedEvents() {
        const int count = 10;

        var commands   = await GenerateAndHandleCommands(count);
        var testEvents = commands.Select(ToEvent).ToList();
        Handler.AssertCollection(2.Seconds(), [..testEvents]);

        await Start();
        await Handler.Validate();
        await Stop();
        Handler.Count.Should().Be(10);
    }

    [Fact]
    public async Task ShouldConsumeProducedEventsWhenRestarting() {
        await TestConsumptionOfProducedEvents();

        Handler.Reset();
        await InitializeAsync();

        await TestConsumptionOfProducedEvents();

        return;

        async Task TestConsumptionOfProducedEvents() {
            const int count = 10;

            var commands   = await GenerateAndHandleCommands(count);
            var testEvents = commands.Select(ToEvent).ToList();
            Handler.AssertCollection(2.Seconds(), [..testEvents]);

            await Start();
            await Handler.Validate();
            await Stop();
            Handler.Count.Should().Be(10);
        }
    }

    [Fact]
    public async Task ShouldUseExistingCheckpoint() {
        const int count = 10;

        await GenerateAndHandleCommands(count);

        await CheckpointStore.GetLastCheckpoint(SubscriptionId, default);
        await CheckpointStore.StoreCheckpoint(new Checkpoint(SubscriptionId, 10), true, default);

        await Start();
        await Task.Delay(TimeSpan.FromSeconds(1));
        await Stop();
        Handler.Count.Should().Be(0);
    }

    static BookingImported ToEvent(ImportBooking cmd) => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    async Task<List<ImportBooking>> GenerateAndHandleCommands(int count) {
        var commands = Enumerable
            .Range(0, count)
            .Select(_ => DomainFixture.CreateImportBooking(Auto))
            .ToList();

        var service = new BookingService(AggregateStore);

        foreach (var cmd in commands) {
            var result = await service.Handle(cmd, default);

            if (result is ErrorResult<BookingState> error) {
                throw error.Exception ?? new Exception(error.Message);
            }
        }

        return commands;
    }

    protected override TestEventHandler GetHandler() => new();
}
