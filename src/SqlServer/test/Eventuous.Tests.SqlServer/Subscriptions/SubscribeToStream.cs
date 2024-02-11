using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Sut.Subs;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.SqlServer.Fixtures;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.SqlServer.Subscriptions;

public class SubscribeToStream(ITestOutputHelper outputHelper) : SubscriptionFixture<TestEventHandler>(outputHelper, new TestEventHandler(), false, false) {
    [Fact]
    public async Task ShouldConsumeProducedEvents() {
        const int count = 10;

        var testEvents = await GenerateAndProduceEvents(count);
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

            var testEvents = await GenerateAndProduceEvents(count);
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

        await GenerateAndProduceEvents(count * 2);

        await CheckpointStore.GetLastCheckpoint(SubscriptionId, default);
        await CheckpointStore.StoreCheckpoint(new Checkpoint(SubscriptionId, 9), true, default);

        await Start();
        await Task.Delay(TimeSpan.FromSeconds(1));
        await Stop();
        Handler.Count.Should().Be(count);
    }

    static BookingImported ToEvent(ImportBooking cmd)
        => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    async Task<List<BookingImported>> GenerateAndProduceEvents(int count) {
        var commands = Enumerable
            .Range(0, count)
            .Select(_ => DomainFixture.CreateImportBooking(Fixture.Auto))
            .ToList();

        var events = commands.Select(ToEvent).ToList();

        var streamEvents = events.Select(x => new StreamEvent(Guid.NewGuid(), x, new Metadata(), "", 0));

        await Fixture.EventStore.AppendEvents(Stream, ExpectedStreamVersion.Any, streamEvents.ToArray(), default);

        return events;
    }
}
