using System.Text.Json;
using ElasticPlayground;
using Elasticsearch.Net;
using EventStore.Client;
using Eventuous.ElasticSearch.Store;
using Eventuous.Sut.Domain;
using Nest;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

TypeMap.RegisterKnownEventTypes(typeof(BookingEvents.RoomBooked).Assembly);

var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);

const string connectionString = "http://localhost:9200";

var settings = new ConnectionSettings(
    new SingleNodeConnectionPool(
        new Uri(Ensure.NotEmptyString(connectionString, "Elasticsearch connection string"))
    ),
    (def, _) => new ElasticSerializer(def, options)
);

var elasticClient = new ElasticClient(settings);

await elasticClient.ConfigureIndex();

var esdbSettings     = EventStoreClientSettings.Create("esdb://localhost:2113?tls=false");
var eventStoreClient = new EventStoreClient(esdbSettings);
DefaultEventSerializer.SetDefaultSerializer(new DefaultEventSerializer(options));

// var elasticOnly = new ElasticOnly(client);
// await elasticOnly.Execute();

// var archived = new CombinedStore(elasticClient, eventStoreClient);
// await archived.Execute();

// var connectorAndArchive = new ConnectorAndArchive(elasticClient, eventStoreClient);
// await connectorAndArchive.Execute();

var connectorAndArchive = new OnlyArchive(elasticClient, eventStoreClient);
await connectorAndArchive.Execute();
