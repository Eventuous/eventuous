using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.Sut.Subs;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.SqlServer.Fixtures;
using Hypothesist;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.SqlServer.Subscriptions;

public class SubscribeToAll(ITestOutputHelper outputHelper) : SubscriptionFixture<TestEventHandler>(outputHelper, new TestEventHandler(), true, false) {
    List<ImportBooking> _commands = null!;

    const int Count = 10;

    [Fact]
    public async Task ShouldConsumeProducedEvents() {
        var testEvents = _commands.Select(ToEvent).ToList();
        Handler.AssertThat().Exactly(Count, x => testEvents.Contains(x));

        await Start();
        await Handler.Validate(2.Seconds());
        Handler.Count.Should().Be(10);
        await Stop();
    }

    [Fact]
    public async Task ShouldConsumeProducedEventsWhenRestarting() {
        await TestConsumptionOfProducedEvents();

        Handler.Reset();
        await InitializeAsync();

        await TestConsumptionOfProducedEvents();

        return;

        async Task TestConsumptionOfProducedEvents() {
            var testEvents = _commands.Select(ToEvent).ToList();
            Handler.AssertThat().Exactly(Count, x => testEvents.Contains(x));

            await Start();
            await Handler.Validate(2.Seconds());
            Handler.Count.Should().Be(10);
            await Stop();
        }
    }

    [Fact]
    public async Task ShouldUseExistingCheckpoint() {
        Handler.AssertThat().Any(_ => true);

        await CheckpointStore.GetLastCheckpoint(SubscriptionId, default);
        await CheckpointStore.StoreCheckpoint(new Checkpoint(SubscriptionId, 9), true, default);

        await Start();
        await Task.Delay(TimeSpan.FromSeconds(1));
        await Stop();
        Handler.Count.Should().Be(0);
    }

    public override async Task InitializeAsync() {
        await base.InitializeAsync();
        _commands = await GenerateAndHandleCommands(Count);
    }

    static BookingImported ToEvent(ImportBooking cmd)
        => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    async Task<List<ImportBooking>> GenerateAndHandleCommands(int count) {
        var commands = Enumerable
            .Range(0, count)
            .Select(_ => DomainFixture.CreateImportBooking(Fixture.Auto))
            .ToList();

        var service = new BookingService(Fixture.AggregateStore);
        foreach (var cmd in commands) {
            var result = await service.Handle(cmd, default);

            if (result is ErrorResult<BookingState> error) {
                throw error.Exception ?? new Exception(error.Message);
            }
        }

        return commands;
    }
}
