using System.Text.Json;
using SqlStreamStore;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Eventuous.SqlStreamStore;

namespace Eventuous.Tests.SqlStreamStore
{
    public class MySqlFixture {
        protected IEventStore EventStore { get; }
        protected IEventSerializer Serializer { get; } = new DefaultEventSerializer(
            new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
        );

        public MySqlFixture() {
            string connectionString = "Server=localhost;Database=myDataBase;Uid=root;Pwd=myPassword;";
            EventStore = new SqlEventStore(new MySqlStreamStore(
                new MySqlStreamStoreSettings(connectionString)
            ));
        }

    }    
}