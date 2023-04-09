using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Logging;
using Eventuous.Sut.Subs;
using Eventuous.Tests.Redis.Fixtures;
using Hypothesist;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Redis.Subscriptions;

[Collection("Sequential")]
public class SubscribeToAll : SubscriptionFixture<TestEventHandler> {
    public SubscribeToAll(ITestOutputHelper outputHelper) : base(outputHelper, true, false) { }

    [Fact]
    public async Task ShouldConsumeProducedEvents() {
        const int count = 10;

        var (testEvents, _) = await GenerateAndProduceEvents(count);
        Handler.AssertThat().Exactly(count, x => testEvents.Contains(x));

        await Start();
        await Handler.Validate(2.Seconds());
        await Stop();

        Handler.Count.Should().Be(10);
    }

    [Fact]
    public async Task ShouldUseExistingCheckpoint() {
        const int count = 10;

        var (_, result) = await GenerateAndProduceEvents(count);
        Handler.AssertThat().Any(_ => true);

        var checkpoint = await CheckpointStore.GetLastCheckpoint(SubscriptionId, default);
        Logger.ConfigureIfNull(SubscriptionId, LoggerFactory);
        await CheckpointStore.StoreCheckpoint(new Checkpoint(SubscriptionId, (ulong)result.GlobalPosition), true, default);

        await Start();
        await Task.Delay(TimeSpan.FromSeconds(1));
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

        var events = commands.Select(ToEvent).ToList();

        var streamEvents = events.Select(x => new StreamEvent(Guid.NewGuid(), x, new Metadata(), "", 0));

        var result = await IntegrationFixture.EventWriter.AppendEvents(
            Stream,
            ExpectedStreamVersion.Any,
            streamEvents.ToArray(),
            default
        );

        return (events, result);
    }

    protected override TestEventHandler GetHandler()
        => new();
}
