using EventStore.Client;
using Eventuous.ElasticSearch.Store;
using Eventuous.EventStore;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Nest;
using static Eventuous.Sut.App.Commands;

namespace ElasticPlayground;

public class OnlyArchive {
    readonly TieredEventStore _tieredEventStore;

    public OnlyArchive(IElasticClient elasticClient, EventStoreClient eventStoreClient) {
        var elasticEventStore = new ElasticEventStore(elasticClient);
        var esdbEventStore    = new EsdbEventStore(eventStoreClient);
        _tieredEventStore = new(esdbEventStore, elasticEventStore);
    }

    public async Task Execute() {
        const string bookingId = "deea3663-17c0-45a6-86b2-70c66fd407fd";

        var service = new ThrowingCommandService<BookingState>(new BookingService(_tieredEventStore));

        var cmd = new RecordPayment(new(bookingId), Generator.RandomString(), new(10), DateTimeOffset.Now);

        var result = await service.Handle(cmd, default);

        result.Dump();
    }
}
