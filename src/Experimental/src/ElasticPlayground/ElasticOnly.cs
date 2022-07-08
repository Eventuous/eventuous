using AutoFixture;
using Eventuous.ElasticSearch.Store;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Nest;
using NodaTime;

namespace ElasticPlayground;

public class ElasticOnly {
    readonly IApplicationService<Booking, BookingState, BookingId> _service;

    static readonly Fixture Fixture = new();

    public ElasticOnly(IElasticClient client) {
        var eventStore = new ElasticEventStore(client);
        var store      = new AggregateStore(eventStore, eventStore);
        _service = new ThrowingApplicationService<Booking, BookingState, BookingId>(new BookingService(store));
    }

    public async Task Execute() {
        var bookRoom = new Commands.BookRoom(
            Fixture.Create<string>(),
            Fixture.Create<string>(),
            LocalDate.FromDateTime(DateTime.Today),
            LocalDate.FromDateTime(DateTime.Today.AddDays(1)),
            100
        );

        var result = await _service.Handle(bookRoom, default);
        result.Dump();

        var processPayment = new Commands.RecordPayment(
            bookRoom.BookingId,
            Fixture.Create<string>(),
            bookRoom.Price,
            DateTimeOffset.Now
        );

        var secondResult = await _service.Handle(processPayment, default);
        secondResult.Dump();
    }
}
