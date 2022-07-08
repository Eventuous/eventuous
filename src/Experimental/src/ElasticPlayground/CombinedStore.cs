using AutoFixture;
using EventStore.Client;
using Eventuous.ElasticSearch.Store;
using Eventuous.EventStore;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Nest;
using NodaTime;
using static Eventuous.Sut.App.Commands;

namespace ElasticPlayground;

public class CombinedStore {
    readonly AggregateStore                    _elasticStore;
    readonly AggregateStore                    _esdbStore;
    readonly AggregateStore<ElasticEventStore> _store;
    readonly EsdbEventStore                    _esdbEventStore;

    static readonly Fixture Fixture = new();

    public CombinedStore(IElasticClient elasticClient, EventStoreClient eventStoreClient) {
        var elasticEventStore = new ElasticEventStore(elasticClient);
        _elasticStore   = new AggregateStore(elasticEventStore, elasticEventStore);
        _esdbEventStore = new EsdbEventStore(eventStoreClient);
        _esdbStore      = new AggregateStore(_esdbEventStore);
        _store          = new AggregateStore<ElasticEventStore>(_esdbEventStore, elasticEventStore);
    }

    public async Task Execute() {
        var bookRoom = new BookRoom(
            Fixture.Create<string>(),
            Fixture.Create<string>(),
            LocalDate.FromDateTime(DateTime.Today),
            LocalDate.FromDateTime(DateTime.Today.AddDays(1)),
            100
        );

        await Seed(_esdbStore, bookRoom);
        await Seed(_elasticStore, bookRoom);

        await _esdbEventStore.TruncateStream(
            StreamName.For<Booking>(bookRoom.BookingId),
            new StreamTruncatePosition(1),
            ExpectedStreamVersion.Any,
            default
        );

        var service = new ThrowingApplicationService<Booking, BookingState, BookingId>(new BookingService(_store));

        var cmd = new RecordPayment(
            bookRoom.BookingId,
            Fixture.Create<string>(),
            bookRoom.Price / 2,
            DateTimeOffset.Now
        );

        var result = await service.Handle(cmd, default);

        result.Dump();
    }

    static async Task Seed(IAggregateStore store, BookRoom bookRoom) {
        var service = new ThrowingApplicationService<Booking, BookingState, BookingId>(new BookingService(store));

        await service.Handle(bookRoom, default);

        var processPayment = new RecordPayment(
            bookRoom.BookingId,
            Fixture.Create<string>(),
            bookRoom.Price / 2,
            DateTimeOffset.Now
        );

        await service.Handle(processPayment, default);
    }
}
