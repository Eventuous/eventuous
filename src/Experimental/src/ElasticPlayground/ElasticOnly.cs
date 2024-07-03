using AutoFixture;
using Eventuous.ElasticSearch.Store;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Nest;
using NodaTime;

namespace ElasticPlayground;

public class ElasticOnly {
    readonly ICommandService<BookingState> _service;

    static readonly Fixture Fixture = new();

    public ElasticOnly(IElasticClient client) {
        var eventStore = new ElasticEventStore(client);
        _service = new ThrowingCommandService<BookingState>(new BookingService(eventStore));
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

        var processPayment = bookRoom.ToRecordPayment(Fixture.Create<string>());

        var secondResult = await _service.Handle(processPayment, default);
        secondResult.Dump();
    }
}
