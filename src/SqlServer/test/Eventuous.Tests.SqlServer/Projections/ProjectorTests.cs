// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.SqlServer;
using Eventuous.SqlServer.Projections;
using Eventuous.SqlServer.Subscriptions;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.SqlServer.Subscriptions;
using Microsoft.Data.SqlClient;

namespace Eventuous.Tests.SqlServer.Projections;

[Collection("Database")]
public class ProjectorTests(ITestOutputHelper outputHelper) : IAsyncLifetime {
    readonly SubscriptionFixture<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions, TestProjector> _fixture
        = new(_ => { }, outputHelper);

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

        await using var connection = await ConnectionFactory.GetConnection(_fixture.ConnectionString, default);

        var select = $"select * from {_fixture.SchemaName}.bookings where booking_id = @bookingId";

        foreach (var command in commands) {
            await using var cmd = new SqlCommand(select, connection);
            cmd.Parameters.AddWithValue("@bookingId", command.BookingId);
            await using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            reader["checkin_date"].Should().Be(command.CheckIn.ToDateTimeUnspecified());
            reader["price"].Should().Be(command.Price);
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
            .Select(_ => DomainFixture.CreateImportBooking(_fixture.Auto))
            .ToList();

        foreach (var command in commands) {
            var evt         = ToEvent(command);
            var streamEvent = new StreamEvent(Guid.NewGuid(), evt, new Metadata(), "", 0);

            await _fixture.EventStore.AppendEvents(
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

    public Task InitializeAsync() => _fixture.InitializeAsync();

    public Task DisposeAsync() => _fixture.DisposeAsync();
}

public class TestProjector : SqlServerProjector {
    public TestProjector(SubscriptionsInfo options, SchemaInfo schemaInfo) : base(options) {
        var insert = $"insert into {schemaInfo.Schema}.bookings (booking_id, checkin_date, price) values (@booking_id, @checkin_date, @price)";

        On<BookingEvents.BookingImported>(
            (connection, ctx) =>
                Project(
                    connection,
                    insert,
                    new SqlParameter("@booking_id", ctx.Stream.GetId()),
                    new SqlParameter("@checkin_date", ctx.Message.CheckIn.ToDateTimeUnspecified()),
                    new SqlParameter("@price", ctx.Message.Price)
                )
        );
    }
}