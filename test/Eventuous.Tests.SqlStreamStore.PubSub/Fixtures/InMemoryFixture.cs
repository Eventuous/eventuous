using System.Threading.Tasks;
using System.Text.Json;
using SqlStreamStore;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Eventuous.Tests.SqlStreamStore.PubSub
{
    public class InMemoryFixture {
        protected IStreamStore StreamStore { get; }
        protected IEventSerializer Serializer { get; } = new DefaultEventSerializer(
            new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
        );

        public InMemoryFixture() {
            StreamStore = new InMemoryStreamStore();
        }

        public Task CleanUp() => Task.CompletedTask;
    }    
}