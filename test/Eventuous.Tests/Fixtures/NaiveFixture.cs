using System.Text.Json;
using Eventuous.Tests.Fakes;
using NodaTime.Serialization.SystemTextJson;

namespace Eventuous.Tests.Fixtures;

public class NaiveFixture {
    protected IEventStore         EventStore     { get; }
    protected IAggregateStore     AggregateStore { get; }
    protected AutoFixture.Fixture Auto           { get; } = new();

    protected IEventSerializer Serializer { get; } = new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    );

    protected NaiveFixture() {
        EventStore     = new InMemoryEventStore();
        AggregateStore = new AggregateStore(EventStore, Serializer);
    }
}