using System;
using System.Data;
using System.Data.SqlClient;
using MySql.Data.MySqlClient;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Eventuous.Subscriptions;
using Eventuous.Projections.Sql;
using FluentAssertions;

namespace Eventuous.Tests.SQLStreamStore
{
    public class CreateMySqlCheckpoints
    {
        readonly MySqlCheckpointStore _checkpointStore;
        public CreateMySqlCheckpoints()
        {
            var connectionString = "Server=localhost;Database=myDataBase;Uid=root;Pwd=myPassword;";
            var connection = new MySqlConnection(connectionString);
            _checkpointStore = new MySqlCheckpointStore(connection, null);
            var task = _checkpointStore.CreateSchemaIfNotExists();
            task.Wait();
        }

        [Fact]
        public async Task GetExistingCheckpoint()
        {
            var checkpointId = "subscription";
            ulong position = 1;
            var checkpoint = new Checkpoint(checkpointId, position);
            await _checkpointStore.StoreCheckpoint(checkpoint);
            var retrievedCheckpoint = await _checkpointStore.GetLastCheckpoint(checkpointId);
            retrievedCheckpoint.Should().BeEquivalentTo(checkpoint);
        }        

        [Fact]
        public async Task CheckpointDoesntExist()
        {
            var checkpointId = "random";
            var checkpoint = await _checkpointStore.GetLastCheckpoint(checkpointId);
            checkpoint.Id.Should().BeEquivalentTo(checkpointId);
            checkpoint.Position.Should().Be(null);
        }        
    }

}