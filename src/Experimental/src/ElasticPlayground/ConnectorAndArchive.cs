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

public class ConnectorAndArchive {
    readonly EsdbEventStore    _esdbEventStore;
    readonly ElasticEventStore _elasticEventStore;
    readonly TieredEventStore  _tieredStore;

    static readonly Fixture Fixture = new();

    public ConnectorAndArchive(IElasticClient elasticClient, EventStoreClient eventStoreClient) {
        _elasticEventStore = new(elasticClient);
        _esdbEventStore    = new(eventStoreClient);
        _tieredStore       = new(_esdbEventStore, _elasticEventStore);
    }

    public async Task Execute() {
        var bookRoom = new BookRoom(
            Fixture.Create<string>(),
            Fixture.Create<string>(),
            LocalDate.FromDateTime(DateTime.Today),
            LocalDate.FromDateTime(DateTime.Today.AddDays(1)),
            100
        );

        await Seed(_elasticEventStore, bookRoom);

        await _esdbEventStore.TruncateStream(
            StreamName.For<Booking>(bookRoom.BookingId),
            new(1),
            ExpectedStreamVersion.Any,
            default
        );

        var service = new ThrowingCommandService<Booking, BookingState, BookingId>(new BookingService(_tieredStore));

        var cmd = bookRoom.ToRecordPayment(Fixture.Create<string>(), 2);

        var result = await service.Handle(cmd, default);

        result.Dump();
    }

    static async Task Seed(IEventStore store, BookRoom bookRoom) {
        var service = new ThrowingCommandService<Booking, BookingState, BookingId>(new BookingService(store));

        await service.Handle(bookRoom, default);

        var processPayment = bookRoom.ToRecordPayment(Fixture.Create<string>(), 2);

        await service.Handle(processPayment, default);
    }
}
