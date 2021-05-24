using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Eventuous.Subscriptions;
using Eventuous.Projections.Sql;
using FluentAssertions;
using Npgsql;

namespace Eventuous.Tests.SQLStreamStore
{
    public class CreatePostgresCheckpoints
    {
        readonly PostgresCheckpointStore _checkpointStore;
        public CreatePostgresCheckpoints()
        {
            var connectionString = "User ID=postgres;Password=myPassword;Host=localhost;Port=5432;Database=mydatabase";
            var schema = "checkpoints";
            var connection = new NpgsqlConnection(connectionString);
            _checkpointStore = new PostgresCheckpointStore(connection, schema, null);
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