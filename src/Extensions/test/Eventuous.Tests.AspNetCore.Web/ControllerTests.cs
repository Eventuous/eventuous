// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Net;
using Eventuous.Sut.AspNetCore;
using Eventuous.Sut.Domain;
using Eventuous.TestHelpers;
using Eventuous.Tests.AspNetCore.Web.Fixture;
using NodaTime;
using RestSharp;

namespace Eventuous.Tests.AspNetCore.Web;

public class ControllerTests {
    readonly ServerFixture       _fixture;
    readonly AutoFixture.Fixture _autoFixture = new();
    readonly TestEventListener   _listener;

    public ControllerTests(ITestOutputHelper output) {
        _fixture  = new ServerFixture();
        _listener = new TestEventListener(output);
    }

    [Fact]
    public async Task RecordPaymentUsingMappedCommand() {
        var service = _fixture.Resolve<ICommandService<Booking>>();

        using var client = _fixture.GetClient();

        var bookRoom = new BookRoom(
            _autoFixture.Create<string>(),
            _autoFixture.Create<string>(),
            LocalDate.FromDateTime(DateTime.Now),
            LocalDate.FromDateTime(DateTime.Now.AddDays(1)),
            100
        );

        var firstResponse = await client.PostJsonAsync("/book", bookRoom);

        var registerPayment = new BookingApi.RegisterPaymentHttp(
            bookRoom.BookingId,
            bookRoom.RoomId,
            100,
            DateTimeOffset.Now
        );

        var request  = new RestRequest("/v2/pay").AddJsonBody(registerPayment);
        var response = await client.ExecutePostAsync<OkResult>(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var expected = new BookingEvents.BookingFullyPaid(registerPayment.PaidAt);

        var events = await _fixture.ReadStream<Booking>(bookRoom.BookingId);
        var last = events.LastOrDefault();
        last?.Payload.Should().BeEquivalentTo(expected);
    }
}
