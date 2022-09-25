using Eventuous.Postgresql;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Logging;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.Sut.Subs;
using Eventuous.Tests.Postgres.Fixtures;
using Hypothesist;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;
using static Eventuous.Tests.Postgres.Fixtures.IntegrationFixture;

namespace Eventuous.Tests.Postgres.Subscriptions;

public class SubscribeToStream : SubscriptionFixture<TestEventHandler> {
    readonly PostgresStore _eventStore;

    public SubscribeToStream(ITestOutputHelper outputHelper)
        : base(outputHelper, new TestEventHandler(), false, false) {
        outputHelper.WriteLine($"Schema: {SchemaName}");

        _eventStore = new PostgresStore(Instance.GetConnection, new PostgresStoreOptions(SchemaName));
    }

    [Fact]
    public async Task ShouldConsumeProducedEvents() {
        const int count = 10;

        var testEvents = await GenerateAndProduceEvents(count);
        Handler.AssertThat().Exactly(count, x => testEvents.Contains(x));

        await Start();
        await Handler.Validate(2.Seconds());
        await Stop();
        Handler.Count.Should().Be(10);
        
        var checkpoint = await CheckpointStore.GetLastCheckpoint(SubscriptionId, default);
        checkpoint.Position.Should().Be(count - 1);
    }

    [Fact]
    public async Task ShouldUseExistingCheckpoint() {
        const int count = 10;
    
        await GenerateAndProduceEvents(count);
        Handler.AssertThat().Any(_ => true);
    
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
            .Select(_ => DomainFixture.CreateImportBooking())
            .ToList();

        var events = commands.Select(ToEvent).ToList();

        var streamEvents = events.Select(x => new StreamEvent(Guid.NewGuid(), x, new Metadata(), "", 0));

        await _eventStore.AppendEvents(
            Stream,
            ExpectedStreamVersion.Any,
            streamEvents.ToArray(),
            default
        );

        return events;
    }
}
