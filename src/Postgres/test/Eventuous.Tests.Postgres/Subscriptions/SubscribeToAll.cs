using Eventuous.Postgresql;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.Sut.Subs;
using Eventuous.Tests.Postgres.Fixtures;
using Hypothesist;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;
using static Eventuous.Tests.Postgres.Fixtures.IntegrationFixture;

namespace Eventuous.Tests.Postgres.Subscriptions;

public class SubscribeToAll : SubscriptionFixture<TestEventHandler> {
    readonly BookingService _service;

    public SubscribeToAll(ITestOutputHelper outputHelper)
        : base(outputHelper, new TestEventHandler(), true, false) {
        outputHelper.WriteLine($"Schema: {SchemaName}");

        var eventStore = new PostgresStore(Instance.GetConnection, new PostgresStoreOptions(SchemaName));
        var store      = new AggregateStore(eventStore);
        _service = new BookingService(store);
    }

    [Fact]
    public async Task ShouldConsumeProducedEvents() {
        const int count = 10;

        var commands   = await GenerateAndHandleCommands(count);
        var testEvents = commands.Select(ToEvent).ToList();
        Handler.AssertThat().Exactly(count, x => testEvents.Contains(x));

        await Start();
        await Handler.Validate(2.Seconds());
        await Stop();
        Handler.Count.Should().Be(10);
    }

    [Fact]
    public async Task ShouldUseExistingCheckpoint() {
        const int count = 10;

        await GenerateAndHandleCommands(count);
        Handler.AssertThat().Any(_ => true);

        await CheckpointStore.GetLastCheckpoint(SubscriptionId, default);
        await CheckpointStore.StoreCheckpoint(new Checkpoint(SubscriptionId, 9), true, default);

        await Start();
        await Task.Delay(TimeSpan.FromSeconds(1));
        await Stop();
        Handler.Count.Should().Be(0);
    }

    static BookingImported ToEvent(ImportBooking cmd) => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    async Task<List<ImportBooking>> GenerateAndHandleCommands(int count) {
        var commands = Enumerable
            .Range(0, count)
            .Select(_ => DomainFixture.CreateImportBooking())
            .ToList();

        foreach (var cmd in commands) {
            var result = await _service.Handle(cmd, default);

            if (result is ErrorResult<BookingState> error) {
                throw error.Exception ?? new Exception(error.Message);
            }
        }

        return commands;
    }
}
