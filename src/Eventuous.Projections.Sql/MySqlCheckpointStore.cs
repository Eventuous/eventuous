using System;
using System.Threading;
using System.Threading.Tasks;
using System.Data;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Dapper;
using Eventuous.Subscriptions;

namespace Eventuous.Projections.Sql {
    [PublicAPI]
    public class MySqlCheckpointStore : ICheckpointStore {
        readonly ILogger<MySqlCheckpointStore>? _log;
        readonly string _table = "Checkpoints";
        readonly IDbConnection _connection;
        public MySqlCheckpointStore(IDbConnection connection, ILogger<MySqlCheckpointStore>? logger) {
            _connection = connection;
            _log = logger;
        }

        public async ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken = default)
        {
            _log?.LogDebug("[{CheckpointId}] Finding checkpoint...", checkpointId);

            var parameters = new { Subscription = checkpointId };
            var sql = $@"
                select Position from {_table} 
                where Subscription = @Subscription 
            "; 
            var position = await _connection.ExecuteScalarAsync<ulong?>(sql, parameters);
            return new Checkpoint(checkpointId, position);
        }

        public async ValueTask<Checkpoint> StoreCheckpoint(Checkpoint checkpoint, CancellationToken cancellationToken = default)
        {
            var sqlSelect = $@"
                select count(*) from {_table} 
                where Subscription = @Subscription 
            "; 
            var result = await _connection.ExecuteScalarAsync<long>(sqlSelect, new { Subscription = checkpoint.Id });

            var parameters = new { Subscription = checkpoint.Id, Position = (long?) checkpoint.Position };
            if (result != 0)
            {
                var sql = $@"
                    update {_table} 
                    set Position = @Position 
                    where Subscription = @Subscription
                ";
                await _connection.ExecuteAsync(sql, parameters);
            }
            else 
            {
                var sql = $@"
                    insert into {_table} (Subscription, Position) values (@Subscription, @Position)
                ";
                await _connection.ExecuteAsync(sql, parameters);
            }
            return checkpoint;
        }

        public async Task CreateSchemaIfNotExists() {
            var sql = $@"
                CREATE TABLE IF NOT EXISTS {_table} (
                    Subscription varchar(255),
                    Position bigint
                );
            ";
            await _connection.ExecuteAsync(sql);
        }

    }
}