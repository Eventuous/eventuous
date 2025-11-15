using Eventuous.ElasticSearch.Store;
using Eventuous.KurrentDB;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using KurrentDB.Client;
using Nest;
using static Eventuous.Sut.App.Commands;

namespace ElasticPlayground;

public class CombinedStore {
    readonly TieredEventStore  _store;
    readonly EsdbEventStore    _esdbEventStore;
    readonly ElasticEventStore _elasticEventStore;

    public CombinedStore(IElasticClient elasticClient, KurrentDBClient kurrentDbClient) {
        _elasticEventStore = new(elasticClient);
        _esdbEventStore    = new(kurrentDbClient);
        _store             = new(_esdbEventStore, _elasticEventStore);
    }

    public async Task Execute() {
        var bookRoom = Generator.CreateBookRoomCommand();

        await Seed(_esdbEventStore, bookRoom);
        await Seed(_elasticEventStore, bookRoom);

        await _esdbEventStore.TruncateStream(StreamName.For<Booking>(bookRoom.BookingId), new(1), ExpectedStreamVersion.Any, default);

        var service = new ThrowingCommandService<BookingState>(new BookingService(_store));

        var cmd = bookRoom.ToRecordPayment(Generator.RandomString(), 2);

        var result = await service.Handle(cmd, default);

        result.Dump();
    }

    static async Task Seed(IEventStore store, BookRoom bookRoom) {
        var service = new ThrowingCommandService<BookingState>(new BookingService(store));

        await service.Handle(bookRoom, default);

        var processPayment = bookRoom.ToRecordPayment(Generator.RandomString(), 2);

        await service.Handle(processPayment, default);
    }
}
