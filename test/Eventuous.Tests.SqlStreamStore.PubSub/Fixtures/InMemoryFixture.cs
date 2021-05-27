using System.Threading.Tasks;
using System.Text.Json;
using SqlStreamStore;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Eventuous.Producers.SqlStreamStore;
using Eventuous.Producers.SqlStreamStore.InMemory;
using Eventuous.Subscriptions.SqlStreamStore;
using Eventuous.Subscriptions.SqlStreamStore.InMemory;

namespace Eventuous.Tests.SqlStreamStore.PubSub
{
    public class InMemoryFixture {

        protected MockEventHandler eventHandler; 

        protected readonly string stream = "stream";
        protected readonly string subscription = "subscription";

        protected readonly SqlStreamStoreProducer producer;
        protected readonly AllStreamSubscription allStreamSubscription;
        protected readonly StreamSubscription streamSubscription; 

        protected IEventSerializer Serializer { get; } = new DefaultEventSerializer(
            new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
        );

        public InMemoryFixture() {

            eventHandler = new MockEventHandler(subscription);
            var streamStore = new InMemoryStreamStore();

            producer = new InMemoryStreamStoreProducer(streamStore, Serializer);

            streamSubscription = new InMemoryStreamSubscription(
                streamStore, 
                stream, 
                subscription, 
                new InMemoryCheckpointStore(), 
                new[] {eventHandler},
                Serializer
            );

            allStreamSubscription = new InMemoryAllStreamSubscription(
                streamStore, 
                subscription,
                new InMemoryCheckpointStore(),
                new[] {eventHandler},
                Serializer
            );

        }

        public Task CleanUp() => Task.CompletedTask;
    }    
}