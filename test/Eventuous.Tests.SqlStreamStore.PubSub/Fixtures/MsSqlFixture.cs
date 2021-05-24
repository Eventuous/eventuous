using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Text.Json;
using SqlStreamStore;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Dapper;

namespace Eventuous.Tests.SqlStreamStore.PubSub
{
    public class MsSqlFixture {
        protected IStreamStore StreamStore { get; }
        protected IEventSerializer Serializer { get; } = new DefaultEventSerializer(
            new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
        );

        readonly string connectionString = "Server=myServerAddress;Database=myDataBase;User Id=myUsername;Password=myPassword;";
        readonly string schema = "test";

        public MsSqlFixture() {

            StreamStore = new MsSqlStreamStoreV3(
                new MsSqlStreamStoreV3Settings(connectionString)
                {
                    Schema = schema
                }
            );
            ((MsSqlStreamStoreV3)StreamStore).CreateSchemaIfNotExists().Wait();
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