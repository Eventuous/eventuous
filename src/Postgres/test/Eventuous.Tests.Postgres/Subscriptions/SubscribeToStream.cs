using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Logging;
using Eventuous.Sut.Subs;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Postgres.Fixtures;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Postgres.Subscriptions;

public class SubscribeToStream : SubscriptionFixture<TestEventHandler> {
    readonly ITestOutputHelper _outputHelper;

    public SubscribeToStream(ITestOutputHelper outputHelper) : base(outputHelper, false, false) {
        _outputHelper = outputHelper;
        outputHelper.WriteLine($"Schema: {SchemaName}");
    }

    [Fact]
    public async Task ShouldConsumeProducedEvents() {
        const int count = 10;

        var testEvents = await GenerateAndProduceEvents(count);
        Handler.AssertCollection(2.Seconds(), [..testEvents]);

        await Start();
        await Handler.Validate();
        await Stop();
        Handler.Count.Should().Be(10);

        var checkpoint = await CheckpointStore.GetLastCheckpoint(SubscriptionId, default);
        checkpoint.Position.Should().Be(count - 1);
    }

    [Fact]
    public async Task ShouldConsumeProducedEventsWhenRestarting() {
        _outputHelper.WriteLine("Phase one");
        await TestConsumptionOfProducedEvents();

        _outputHelper.WriteLine("Resetting handler");
        Handler.Reset();
        // await InitializeAsync();

        _outputHelper.WriteLine("Phase two");
        await TestConsumptionOfProducedEvents();

        var checkpoint = await CheckpointStore.GetLastCheckpoint(SubscriptionId, default);
        checkpoint.Position.Should().Be(19);

        return;

        async Task TestConsumptionOfProducedEvents() {
            const int count = 10;

            _outputHelper.WriteLine("Generating and producing events");
            var testEvents = await GenerateAndProduceEvents(count);
            Handler.AssertCollection(2.Seconds(), [..testEvents]);

            _outputHelper.WriteLine("Starting subscription");
            await Start();
            await Handler.Validate();
            _outputHelper.WriteLine("Stopping subscription");
            await Stop();
            Handler.Count.Should().Be(10);
        }
    }

    [Fact]
    public async Task ShouldUseExistingCheckpoint() {
        const int count = 10;

        await GenerateAndProduceEvents(count);

        await CheckpointStore.GetLastCheckpoint(SubscriptionId, default);
        Logger.ConfigureIfNull(SubscriptionId, LoggerFactory);
        await CheckpointStore.StoreCheckpoint(new Checkpoint(SubscriptionId, 9), true, default);

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
            .Select(_ => DomainFixture.CreateImportBooking(Auto))
            .ToList();

        var events       = commands.Select(ToEvent).ToList();
        var streamEvents = events.Select(x => new StreamEvent(Guid.NewGuid(), x, new Metadata(), "", 0));
        await EventStore.AppendEvents(Stream, ExpectedStreamVersion.Any, streamEvents.ToArray(), default);

        return events;
    }

    protected override TestEventHandler GetHandler() => new();
}
