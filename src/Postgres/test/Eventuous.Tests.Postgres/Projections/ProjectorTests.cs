// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Postgresql.Projections;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Postgres.Fixtures;
using Npgsql;

namespace Eventuous.Tests.Postgres.Projections;

public class ProjectorTests(ITestOutputHelper outputHelper) : SubscriptionFixture<TestProjector>(outputHelper, true) {
    const string Schema = """
                          create table if not exists __schema__.bookings (
                              booking_id varchar(1000) not null primary key,
                              checkin_date timestamp,
                              price numeric(10,2)
                          );
                          """;

    [Fact]
    public async Task ProjectImportedBookingsToTable() {
        await CreateSchema();
        var commands = await GenerateAndProduceEvents(100);

        await Task.Delay(1000);

        await using var connection = await DataSource.OpenConnectionAsync();

        var select = $"select * from {SchemaName}.bookings where booking_id = @bookingId";

        foreach (var command in commands) {
            await using var cmd = new NpgsqlCommand(select, connection);
            cmd.Parameters.AddWithValue("@bookingId", command.BookingId);
            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            reader["checkin_date"].Should().Be(command.CheckIn.ToDateTimeUnspecified());
            reader["price"].Should().Be(command.Price);
        }
    }

    async Task CreateSchema() {
        await using var connection = await DataSource.OpenConnectionAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = Schema.Replace("__schema__", SchemaName);
        await cmd.ExecuteNonQueryAsync();
    }

    protected override TestProjector GetHandler() => new(DataSource, SchemaName);

    async Task<List<Commands.ImportBooking>> GenerateAndProduceEvents(int count) {
        var commands = Enumerable
            .Range(0, count)
            .Select(_ => DomainFixture.CreateImportBooking(Auto))
            .ToList();

        foreach (var command in commands) {
            var evt         = ToEvent(command);
            var streamEvent = new StreamEvent(Guid.NewGuid(), evt, new Metadata(), "", 0);

            await EventStore.AppendEvents(
                StreamName.For<Booking>(command.BookingId),
                ExpectedStreamVersion.NoStream,
                new[] { streamEvent },
                default
            );
        }

        return commands;
    }

    static BookingEvents.BookingImported ToEvent(Commands.ImportBooking cmd)
        => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);
}

public class TestProjector : PostgresProjector {
    public TestProjector(NpgsqlDataSource dataSource, string schema) : base(dataSource) {
        var insert = $"insert into {schema}.bookings (booking_id, checkin_date, price) values (@booking_id, @checkin_date, @price)";

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
