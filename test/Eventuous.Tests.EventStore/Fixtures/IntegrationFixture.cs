using EventStore.Client;
using Eventuous.EventStoreDB;

namespace Eventuous.Tests.EventStore.Fixtures {
    public class IntegrationFixture {
        public IEventStore         EventStore     { get; }
        public IAggregateStore     AggregateStore { get; }
        public EventStoreClient    Client         { get; }
        public AutoFixture.Fixture Auto           { get; } = new();

        public static IntegrationFixture Instance { get; } = new();

        public IntegrationFixture() {
            var settings = EventStoreClientSettings.Create("esdb://localhost:2113?tls=false");
            Client         = new EventStoreClient(settings);
            EventStore     = new EsdbEventStore(Client);
            AggregateStore = new AggregateStore(EventStore, Serializer.Json);
        }
    }
}