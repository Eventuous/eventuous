using System.Text.Json;
using EventStore.Client;
using Eventuous.EventStoreDB;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Eventuous.Tests.Fixtures {
    public class IntegrationFixture {
        public IEventStore         EventStore     { get; }
        public IAggregateStore     AggregateStore { get; }
        public AutoFixture.Fixture Auto           { get; } = new();

        public IEventSerializer Serializer { get; } = new DefaultEventSerializer(
            new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
        );

        public IntegrationFixture() {
            var settings = EventStoreClientSettings.Create("esdb://localhost:2113?tls=false");
            var client   = new EventStoreClient(settings);
            EventStore     = new EsDbEventStore(client);
            AggregateStore = new AggregateStore(EventStore, Serializer);
        }
    }
}