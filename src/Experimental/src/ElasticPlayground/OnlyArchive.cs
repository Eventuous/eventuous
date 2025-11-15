using Eventuous.ElasticSearch.Store;
using Eventuous.KurrentDB;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using KurrentDB.Client;
using Nest;
using static Eventuous.Sut.App.Commands;

namespace ElasticPlayground;

public class OnlyArchive {
    readonly TieredEventStore _tieredEventStore;

    public OnlyArchive(IElasticClient elasticClient, KurrentDBClient kurrentDBClient) {
        var elasticEventStore = new ElasticEventStore(elasticClient);
        var esdbEventStore    = new EsdbEventStore(kurrentDBClient);
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
