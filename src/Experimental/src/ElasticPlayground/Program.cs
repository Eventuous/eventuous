using System.Text.Json;
using AutoFixture;
using ElasticPlayground;
using Elasticsearch.Net;
using Eventuous.ElasticSearch.Store;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Nest;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

TypeMap.RegisterKnownEventTypes(typeof(BookingEvents.RoomBooked).Assembly);
var fixture = new Fixture();

var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

const string connectionString = "http://localhost:9200";

var settings = new ConnectionSettings(
    new SingleNodeConnectionPool(
        new Uri(Ensure.NotEmptyString(connectionString, "Elasticsearch connection string"))
    ),
    (def, _) => new ElasticSerializer(def, options)
);

var client = new ElasticClient(settings);

await client.ConfigureIndex();

var eventStore = new ElasticEventStore(client);
var store      = new AggregateStore(eventStore);

var service = new ThrowingApplicationService<Booking, BookingState, BookingId>(new BookingService(store));

var bookRoom = new Commands.BookRoom(
    fixture.Create<string>(),
    fixture.Create<string>(),
    LocalDate.FromDateTime(DateTime.Today),
    LocalDate.FromDateTime(DateTime.Today.AddDays(1)),
    100
);

var result = await service.Handle(bookRoom, default);
DumpResult(result);

var processPayment = new Commands.RecordPayment(bookRoom.BookingId, fixture.Create<string>(), bookRoom.Price);
var secondResult   = await service.Handle(processPayment, default);
DumpResult(secondResult);

void DumpResult(Result<BookingState, BookingId> r) {
    Console.WriteLine(r.Success ? "Success" : "Failure");

    switch (r) {
        case OkResult<BookingState, BookingId> ok:
            foreach (var change in ok.Changes!) {
                Console.WriteLine($"{change.EventType} {change.Event}");
            }
            break;
        case ErrorResult<BookingState, BookingId> error:
            Console.WriteLine(error.ErrorMessage);
            break;
    }
}