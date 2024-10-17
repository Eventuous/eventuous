using Eventuous.Subscriptions.Logging;
using Eventuous.Tests.Redis.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;
using static Xunit.TestContext;

namespace Eventuous.Tests.Redis.Subscriptions;

public class SubscribeToAll(ITestOutputHelper outputHelper) : SubscriptionFixture<TestEventHandler>(outputHelper, true, false) {
    [Fact]
    public async Task ShouldConsumeProducedEvents() {
        const int count = 10;

        var (testEvents, _) = await GenerateAndProduceEvents(count);

        await Start();
        await Handler.AssertThat().Timebox(2.Seconds()).Exactly(count).Match(x => testEvents.Contains(x)).Validate(Current.CancellationToken);
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

            var (testEvents, _) = await GenerateAndProduceEvents(count);

            await Start();
            await Handler.AssertCollection(2.Seconds(), [..testEvents]).Validate(Current.CancellationToken);
            await Stop();

            Handler.Count.Should().Be(10);
        }
    }

    [Fact]
    public async Task ShouldUseExistingCheckpoint() {
        const int count = 10;

        var (_, result) = await GenerateAndProduceEvents(count);

        await CheckpointStore.GetLastCheckpoint(SubscriptionId, Current.CancellationToken);
        Logger.ConfigureIfNull(SubscriptionId, LoggerFactory);
        await CheckpointStore.StoreCheckpoint(new(SubscriptionId, result.GlobalPosition), true, Current.CancellationToken);

        await Start();
        await Task.Delay(TimeSpan.FromSeconds(1), Current.CancellationToken);
        await Stop();
        Handler.Count.Should().Be(0);
    }

    static BookingImported ToEvent(ImportBooking cmd)
        => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    async Task<(List<BookingImported>, AppendEventsResult)> GenerateAndProduceEvents(int count) {
        var commands = Enumerable
            .Range(0, count)
            .Select(_ => DomainFixture.CreateImportBooking())
            .ToList();

        var events       = commands.Select(ToEvent).ToList();
        var streamEvents = events.Select(x => new NewStreamEvent(Guid.NewGuid(), x, new()));
        var result       = await IntegrationFixture.EventWriter.AppendEvents(Stream, ExpectedStreamVersion.Any, streamEvents.ToArray(), default);

        return (events, result);
    }

    protected override TestEventHandler GetHandler() => new();
}
