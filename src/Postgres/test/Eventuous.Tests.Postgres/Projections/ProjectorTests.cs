using Eventuous.Postgresql.Projections;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Postgres.Subscriptions;
using Npgsql;
using Assert = TUnit.Assertions.Assert;

namespace Eventuous.Tests.Postgres.Projections;

public class ProjectorTests() {
    readonly SubscriptionFixture<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, TestProjector> _fixture = new(_ => { });

    const string Schema = """
                          create table if not exists __schema__.bookings (
                              booking_id varchar(1000) not null primary key,
                              checkin_date timestamp,
                              price numeric(10,2)
                          );
                          """;

    [Test]
    public async Task ProjectImportedBookingsToTable(CancellationToken cancellationToken) {
        await CreateSchema();
        var commands = await GenerateAndProduceEvents(100);

        await Task.Delay(1000, cancellationToken);

        await using var connection = await _fixture.DataSource.OpenConnectionAsync(cancellationToken);

        var select = $"select * from {_fixture.SchemaName}.bookings where booking_id = @bookingId";

        foreach (var command in commands) {
            await using var cmd = new NpgsqlCommand(select, connection);
            cmd.Parameters.AddWithValue("@bookingId", command.BookingId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            await Assert.That(reader["checkin_date"]).IsEqualTo(command.CheckIn.ToDateTimeUnspecified());
            await Assert.That(reader["price"]).IsEqualTo((decimal)command.Price);
        }
    }

    async Task CreateSchema() {
        await using var connection = await _fixture.DataSource.OpenConnectionAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = Schema.Replace("__schema__", _fixture.SchemaName);
        await cmd.ExecuteNonQueryAsync();
    }

    async Task<List<Commands.ImportBooking>> GenerateAndProduceEvents(int count) {
        var commands = Enumerable
            .Range(0, count)
            .Select(_ => DomainFixture.CreateImportBooking(_fixture.Auto))
            .ToList();

        foreach (var command in commands) {
            var evt         = ToEvent(command);
            var streamEvent = new NewStreamEvent(Guid.NewGuid(), evt, new());

            await _fixture.EventStore.AppendEvents(StreamName.For<Booking>(command.BookingId), ExpectedStreamVersion.NoStream, [streamEvent], default);
        }

        return commands;
    }

    static BookingEvents.BookingImported ToEvent(Commands.ImportBooking cmd)
        => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    [Before(Test)]
    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    [After(Test)]
    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();
}

public class TestProjector : PostgresProjector {
    public TestProjector(NpgsqlDataSource dataSource, SchemaInfo schemaInfo) : base(dataSource) {
        var insert = $"insert into {schemaInfo.Schema}.bookings (booking_id, checkin_date, price) values (@booking_id, @checkin_date, @price)";

        On<BookingEvents.BookingImported>(
            (connection, ctx) =>
                Project(
                    connection,
                    insert,
                    new NpgsqlParameter("@booking_id", ctx.Stream.GetId()),
                    new NpgsqlParameter("@checkin_date", ctx.Message.CheckIn.ToDateTimeUnspecified()),
                    new NpgsqlParameter("@price", ctx.Message.Price)
                )
        );
    }
}
