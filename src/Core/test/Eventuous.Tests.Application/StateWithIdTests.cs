// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.TestHelpers;
using Eventuous.TestHelpers.Fakes;
using NodaTime;

namespace Eventuous.Tests.Application;

public class StateWithIdTests {
    readonly IEventStore    _store;
    readonly BookingService _service;
    readonly AggregateStore _aggregateStore;

    public StateWithIdTests() {
        _store          = new InMemoryEventStore();
        _aggregateStore = new AggregateStore(_store, memoryCache: Caching.CreateMemoryCache());
        _service        = new BookingService(_aggregateStore);
        TypeMap.RegisterKnownEventTypes(typeof(BookingEvents).Assembly);
    }

    [Fact]
    public async Task ShouldGetIdForNew() {
        var map       = new StreamNameMap();
        var bookingId = new BookingId(Guid.NewGuid().ToString());
        var result    = await Seed(bookingId);

        // Ensure that the id was set when the aggregate was created
        result.State!.Id.Should().Be(bookingId);

        var instance = await _aggregateStore.Load<Booking, BookingState, BookingId>(map, bookingId, default);

        // Ensure that the id was set when the aggregate was loaded
        instance.State.Id.Should().Be(bookingId);
    }

    [Fact]
    public async Task ExpectedEventsForBookingAndPayment() {
        var bookingId = new BookingId(Guid.NewGuid().ToString());
        var result    = await Seed(bookingId);
        result.Changes.Should().HaveCount(1);

        result = await _service.Handle(new Commands.RecordPayment(bookingId, "payment1", new(100), DateTimeOffset.Now), default);
        result.Changes.Should().HaveCount(3);
    }

    async Task<Result<BookingState>> Seed(string id) {
        var checkIn  = LocalDate.FromDateTime(DateTime.Today);
        var checkOut = checkIn.PlusDays(1);
        var cmd      = new Commands.BookRoom(id, "234", checkIn, checkOut, 100);

        return await _service.Handle(cmd, default);
    }
}
