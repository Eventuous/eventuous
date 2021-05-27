using System.Text.Json;
using SqlStreamStore;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Eventuous.SqlStreamStore;
using Eventuous.SqlStreamStore.InMemory;

namespace Eventuous.Tests.SqlStreamStore
{
    public class InMemoryFixture {
        protected IEventStore EventStore { get; }
        protected IEventSerializer Serializer { get; } = new DefaultEventSerializer(
            new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
        );

        public InMemoryFixture() {
            EventStore = new InMemoryEventStore(new InMemoryStreamStore());
        }

    }    
}