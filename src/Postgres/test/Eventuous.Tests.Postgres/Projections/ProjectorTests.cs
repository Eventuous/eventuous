// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Postgresql;
using Eventuous.Postgresql.Projections;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Postgres.Fixtures;
using Npgsql;

namespace Eventuous.Tests.Postgres.Projections;

public class ProjectorTests : SubscriptionFixture<TestProjector> {
    const string Schema = @"
create table if not exists __schema__.bookings (
    booking_id varchar(1000) not null primary key,
    checkin_date timestamp,
    price numeric(10,2)
);";

    public ProjectorTests(ITestOutputHelper outputHelper) : base(outputHelper, true) { }

    [Fact]
    public async Task ProjectImportedBookingsToTable() {
        await CreateSchema();
        var commands = await GenerateAndProduceEvents(100);

        await Task.Delay(1000);

        await using var connection = IntegrationFixture.GetConnection();
        await connection.OpenAsync();

        var select = $"select * from {IntegrationFixture.SchemaName}.bookings where booking_id = @bookingId";

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
        await using var connection = IntegrationFixture.GetConnection();

        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = Schema.Replace("__schema__", IntegrationFixture.SchemaName);
        await cmd.ExecuteNonQueryAsync();
    }

    protected override TestProjector GetHandler()
        => new(IntegrationFixture.GetConnection, IntegrationFixture.SchemaName);

    async Task<List<Commands.ImportBooking>> GenerateAndProduceEvents(int count) {
        var commands = Enumerable
            .Range(0, count)
            .Select(_ => DomainFixture.CreateImportBooking())
            .ToList();

        foreach (var command in commands) {
            var evt         = ToEvent(command);
            var streamEvent = new StreamEvent(Guid.NewGuid(), evt, new Metadata(), "", 0);

            await IntegrationFixture.EventStore.AppendEvents(
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
    public TestProjector(GetPostgresConnection getConnection, string schema) : base(getConnection) {
        var insert = $"insert into {schema}.bookings (booking_id, checkin_date, price) values (@booking_id, @checkin_date, @price)";

        On<BookingEvents.BookingImported>(
            (connection, ctx) => ValueTask.FromResult(
                Project(
                    connection,
                    insert,
                    new NpgsqlParameter("@booking_id", ctx.Stream.GetId()),
                    new NpgsqlParameter("@checkin_date", ctx.Message.CheckIn.ToDateTimeUnspecified()),
                    new NpgsqlParameter("@price", ctx.Message.Price)
                )
            )
        );
    }
}
