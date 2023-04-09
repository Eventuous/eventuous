using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Logging;
using Eventuous.Sut.Subs;
using Eventuous.Tests.Redis.Fixtures;
using Hypothesist;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Redis.Subscriptions;

[Collection("Sequential")]
public class SubscribeToStream : SubscriptionFixture<TestEventHandler> {
    public SubscribeToStream(ITestOutputHelper outputHelper) : base(outputHelper, false, false) { }

    [Fact]
    public async Task ShouldConsumeProducedEvents() {
        const int count = 10;

        var testEvents = await GenerateAndProduceEvents(count);
        Handler.AssertThat().Exactly(count, x => testEvents.Contains(x));

        await Start();
        await Handler.Validate(2.Seconds());
        await Stop();

        Handler.Count.Should().Be(10);
    }

    [Fact]
    public async Task ShouldUseExistingCheckpoint() {
        const int count = 10;

        await GenerateAndProduceEvents(count);
        Handler.AssertThat().Any(_ => true);

        await CheckpointStore.GetLastCheckpoint(SubscriptionId, default);
        var streamPosition = await GetStreamPosition(count);
        Logger.ConfigureIfNull(SubscriptionId, LoggerFactory);
        await CheckpointStore.StoreCheckpoint(new Checkpoint(SubscriptionId, (ulong)streamPosition), true, default);

        await Start();
        await Task.Delay(TimeSpan.FromSeconds(1));
        await Stop();
        Handler.Count.Should().Be(0);
    }

    static BookingImported ToEvent(ImportBooking cmd)
        => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    async Task<List<BookingImported>> GenerateAndProduceEvents(int count) {
        var commands = Enumerable
            .Range(0, count)
            .Select(_ => DomainFixture.CreateImportBooking())
            .ToList();

        var events = commands.Select(ToEvent).ToList();

        var streamEvents = events.Select(x => new StreamEvent(Guid.NewGuid(), x, new Metadata(), "", 0));

        await IntegrationFixture.EventWriter.AppendEvents(
            Stream,
            ExpectedStreamVersion.Any,
            streamEvents.ToArray(),
            default
        );

        return events;
    }

    async Task<long> GetStreamPosition(int count) {
        var readEvents = await IntegrationFixture.EventReader.ReadEvents(
            Stream,
            StreamReadPosition.Start,
            count,
            default
        );

        return readEvents.Last().Position;
    }

    protected override TestEventHandler GetHandler()
        => new();
}
