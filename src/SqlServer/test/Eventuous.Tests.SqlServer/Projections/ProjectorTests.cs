using Eventuous.SqlServer;
using Eventuous.SqlServer.Projections;
using Eventuous.SqlServer.Subscriptions;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.SqlServer.Subscriptions;
using Microsoft.Data.SqlClient;

namespace Eventuous.Tests.SqlServer.Projections;

public class ProjectorTests() {
    readonly SubscriptionFixture<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions, TestProjector> _fixture
        = new(_ => { });

    const string Schema = """
                          IF OBJECT_ID('__schema__.Bookings', 'U') IS NULL
                          BEGIN
                              CREATE TABLE __schema__.Bookings (
                                  BookingId VARCHAR(1000) NOT NULL PRIMARY KEY,
                                  CheckinDate DATETIME2,
                                  Price NUMERIC(10,2)
                              );
                          END
                          """;

    [Test]
    public async Task ProjectImportedBookingsToTable(CancellationToken cancellationToken) {
        await CreateSchema();
        var commands = await GenerateAndProduceEvents(100);

        await Task.Delay(1000, cancellationToken);

        await using var connection = await ConnectionFactory.GetConnection(_fixture.ConnectionString, cancellationToken);

        var select = $"SELECT * FROM {_fixture.SchemaName}.Bookings where BookingId = @BookingId";

        foreach (var command in commands) {
            await ValidateProjectedObject(connection, command);
        }

        return;

        async Task ValidateProjectedObject(SqlConnection conn, Commands.ImportBooking command) {
            await using var cmd = new SqlCommand(select, conn);
            cmd.Parameters.AddWithValue("@BookingId", command.BookingId);
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            reader["CheckinDate"].Should().Be(command.CheckIn.ToDateTimeUnspecified());
            reader["Price"].Should().Be(command.Price);
        }
    }

    async Task CreateSchema() {
        await using var connection = await ConnectionFactory.GetConnection(_fixture.ConnectionString, default);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = Schema.Replace("__schema__", _fixture.SchemaName);
        await cmd.ExecuteNonQueryAsync();
    }

    async Task<List<Commands.ImportBooking>> GenerateAndProduceEvents(int count) {
        var commands = Enumerable
            .Range(0, count)
            .Select(_ => DomainFixture.CreateImportBooking())
            .ToList();

        foreach (var command in commands) {
            var evt         = ToEvent(command);
            var streamEvent = new NewStreamEvent(Guid.NewGuid(), evt, new());
            await _fixture.EventStore.AppendEvents(StreamName.For<Booking>(command.BookingId), ExpectedStreamVersion.NoStream, [streamEvent], default);
        }

        return commands;
    }

    static BookingEvents.BookingImported ToEvent(Commands.ImportBooking cmd) => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    [Before(Test)]
    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    [After(Test)]
    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();
}

public class TestProjector : SqlServerProjector {
    public TestProjector(SqlServerConnectionOptions options, SchemaInfo schemaInfo) : base(options) {
        var insert = $"""
                      INSERT INTO {schemaInfo.Schema}.Bookings 
                      (BookingId, CheckinDate, Price) 
                      values (@BookingId, @CheckinDate, @Price)
                      """;

        On<BookingEvents.BookingImported>(
            (connection, ctx) =>
                Project(
                    connection,
                    insert,
                    new SqlParameter("@BookingId", ctx.Stream.GetId()),
                    new SqlParameter("@CheckinDate", ctx.Message.CheckIn.ToDateTimeUnspecified()),
                    new SqlParameter("@Price", ctx.Message.Price)
                )
        );
    }
}
