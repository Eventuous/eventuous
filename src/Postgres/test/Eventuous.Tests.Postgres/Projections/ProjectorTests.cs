// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Postgresql.Projections;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Postgres.Subscriptions;
using Npgsql;

namespace Eventuous.Tests.Postgres.Projections;

[Collection("Database")]
public class ProjectorTests(ITestOutputHelper outputHelper) : IAsyncLifetime {
    readonly SubscriptionFixture<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, TestProjector> _fixture = new(_ => { }, outputHelper);

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

        await using var connection = await _fixture.DataSource.OpenConnectionAsync();

        var select = $"select * from {_fixture.SchemaName}.bookings where booking_id = @bookingId";

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

    public Task InitializeAsync() => _fixture.InitializeAsync();

    public Task DisposeAsync() => _fixture.DisposeAsync();
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
