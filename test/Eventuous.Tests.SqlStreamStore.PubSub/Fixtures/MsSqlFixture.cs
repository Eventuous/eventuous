using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Text.Json;
using SqlStreamStore;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Dapper;
using Eventuous.Producers.SqlStreamStore;
using Eventuous.Producers.SqlStreamStore.MsSql;
using Eventuous.Subscriptions.SqlStreamStore;
using Eventuous.Subscriptions.SqlStreamStore.MsSql;

namespace Eventuous.Tests.SqlStreamStore.PubSub
{
    public class MsSqlFixture {
        protected MockEventHandler eventHandler; 
        protected readonly string stream = "stream";
        protected readonly string subscription = "subscription";
        protected readonly SqlStreamStoreProducer producer;
        protected readonly AllStreamSubscription allStreamSubscription;
        protected readonly StreamSubscription streamSubscription; 
        protected IEventSerializer Serializer { get; } = new DefaultEventSerializer(
            new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
        );

        readonly string connectionString = "Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;";
        readonly string schema = "test";

        public MsSqlFixture() {

            eventHandler = new MockEventHandler(subscription);
            var streamStore = new MsSqlStreamStoreV3(
                new MsSqlStreamStoreV3Settings(connectionString)
                {
                    Schema = schema
                }
            );
            streamStore.CreateSchemaIfNotExists().Wait();

            producer = new MsSqlStreamStoreProducer(streamStore, Serializer);

            streamSubscription = new MsSqlStreamSubscription(
                streamStore, 
                stream, 
                subscription, 
                new InMemoryCheckpointStore(), 
                new[] {eventHandler},
                Serializer
            );

            allStreamSubscription = new MsSqlAllStreamSubscription(
                streamStore, 
                subscription,
                new InMemoryCheckpointStore(),
                new[] {eventHandler},
                Serializer
            );

        }

        public async Task CleanUp() {
            var connection = new SqlConnection(connectionString);
            var sql = $@"
                delete from {schema}.messages;
                delete from {schema}.streams;
            ";
            await connection.ExecuteAsync(sql);            
        }

    }    
}