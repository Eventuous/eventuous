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

public class OnlyArchive {
    readonly AggregateStore<ElasticEventStore> _store;

    static readonly Fixture Fixture = new();

    public OnlyArchive(IElasticClient elasticClient, EventStoreClient eventStoreClient) {
        var elasticEventStore = new ElasticEventStore(elasticClient);
        var esdbEventStore    = new EsdbEventStore(eventStoreClient);
        _store = new AggregateStore<ElasticEventStore>(esdbEventStore, elasticEventStore);
    }

    public async Task Execute() {
        var bookingId = "deea3663-17c0-45a6-86b2-70c66fd407fd";

        var service = new ThrowingApplicationService<Booking, BookingState, BookingId>(new BookingService(_store));

        var cmd = new RecordPayment(bookingId, Fixture.Create<string>(), 10, DateTimeOffset.Now);

        var result = await service.Handle(cmd, default);

        result.Dump();
    }
}
