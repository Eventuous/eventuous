using Eventuous.ElasticSearch.Store;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Nest;

namespace ElasticPlayground;

public class ElasticOnly {
    readonly ICommandService<BookingState> _service;

    public ElasticOnly(IElasticClient client) {
        var eventStore = new ElasticEventStore(client);
        _service = new ThrowingCommandService<BookingState>(new BookingService(eventStore));
    }

    public async Task Execute() {
        var bookRoom = Generator.CreateBookRoomCommand();
        var result   = await _service.Handle(bookRoom, default);
        result.Dump();

        var processPayment = bookRoom.ToRecordPayment(Generator.RandomString());

        var secondResult = await _service.Handle(processPayment, default);
        secondResult.Dump();
    }
}
