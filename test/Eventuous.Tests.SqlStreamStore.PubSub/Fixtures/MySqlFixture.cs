using System;
using System.Threading.Tasks;
using System.Text.Json;
using SqlStreamStore;
using System.Data.SqlClient;
using MySql.Data.MySqlClient;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Dapper;

namespace Eventuous.Tests.SqlStreamStore.PubSub
{
    public class MySqlFixture {
        protected IStreamStore StreamStore { get; }
        protected IEventSerializer Serializer { get; } = new DefaultEventSerializer(
            new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
        );
        readonly string connectionString = "Server=localhost;Database=myDataBase;Uid=root;Pwd=myPassword;";

        public MySqlFixture() {
            StreamStore = new MySqlStreamStore(
                new MySqlStreamStoreSettings(connectionString)
            );

            ((MySqlStreamStore)StreamStore).CreateSchemaIfNotExists().Wait();
        }

        public async Task CleanUp() {
            var connection = new MySqlConnection(connectionString);
            var sql = $@"
                delete from messages;
                delete from streams;
            ";
            await connection.ExecuteAsync(sql);            
        }

    }    
}