using System.Data;
using Eventuous.SqlServer;
using Eventuous.SqlServer.Extensions;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.Sut.Subs;
using Eventuous.Tests.SqlServer.Fixtures;
using Hypothesist;

namespace Eventuous.Tests.SqlServer.Subscriptions;

public class LoadTest : SubscriptionFixture<TestEventHandler> {
    readonly IntegrationFixture _fixture;
    readonly BookingService     _service;

    public LoadTest(IntegrationFixture fixture, ITestOutputHelper output)
        : base(fixture, output, new TestEventHandler(), true, autoStart: false, logLevel: LogLevel.Debug) {
        _fixture = fixture;
        var eventStore = new SqlServerStore(fixture.GetConnection, new SqlServerStoreOptions(SchemaName));
        var store      = new AggregateStore(eventStore);
        _service = new BookingService(store);
    }

    [Fact]
    public async Task ProduceAndConsumeManyEvents() {
        const int count = 55000;
        Handler.AssertThat().Any(_ => true);

        var generateTask = Task.Run(() => GenerateAndHandleCommands(count));

        await Start();
        await Task.Delay(TimeSpan.FromMinutes(7));
        await Stop();
        Handler.Count.Should().Be(count);

        var checkpoint = await CheckpointStore.GetLastCheckpoint(SubscriptionId, default);
        checkpoint.Position.Value.Should().Be(count - 1);

        await using var connection = _fixture.GetConnection();
        await connection.OpenAsync();

        await using var cmd = connection.GetStoredProcCommand(Schema.ReadAllBackwards)
            .Add("@from_position", SqlDbType.BigInt, long.MaxValue)
            .Add("@count", SqlDbType.Int, 1);
        await using var reader = await cmd.ExecuteReaderAsync(CancellationToken.None);

        var result = reader.ReadEvents(CancellationToken.None);

        var lastEvent = await result.LastAsync();
        lastEvent.GlobalPosition.Should().Be(count - 1);
    }

    async Task<List<Commands.ImportBooking>> GenerateAndHandleCommands(int count) {
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

    static BookingEvents.BookingImported ToEvent(Commands.ImportBooking cmd)
        => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);
}
