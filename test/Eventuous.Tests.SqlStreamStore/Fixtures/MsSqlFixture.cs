using System.Text.Json;
using SqlStreamStore;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Eventuous.SqlStreamStore;
using Eventuous.SqlStreamStore.MsSql;

namespace Eventuous.Tests.SqlStreamStore
{
    public class MsSqlFixture {
        protected IEventStore EventStore { get; }
        protected IEventSerializer Serializer { get; } = new DefaultEventSerializer(
            new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
        );

        public MsSqlFixture() {
            string connectionString = "Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;";
            string schema = "events";
            var mssqlStreamStore = new MsSqlStreamStoreV3(
                new MsSqlStreamStoreV3Settings(connectionString)
                {
                    Schema = schema
                }
            );
            EventStore = new MsSqlEventStore(mssqlStreamStore);

            mssqlStreamStore.CreateSchemaIfNotExists().Wait();
        }

    }    
}