using System;
using System.Threading;
using System.Threading.Tasks;
using System.Data;
using Microsoft.Extensions.Logging;
using Dapper;
using Eventuous.Subscriptions;
using JetBrains.Annotations;

namespace Eventuous.Projections.Sql {
    [PublicAPI]
    public class PostgresCheckpointStore : ICheckpointStore {
        readonly ILogger<PostgresCheckpointStore>? _log;
        readonly string _schema;
        readonly string _table = "Checkpoints";
        readonly IDbConnection _connection;
        public PostgresCheckpointStore(IDbConnection connection, string schema, ILogger<PostgresCheckpointStore>? logger) {
            _connection = connection;
            _schema = schema ?? "public";
            _log = logger;
        }

        public async ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken = default)
        {
            _log?.LogDebug("[{CheckpointId}] Finding checkpoint...", checkpointId);

            var parameters = new { Subscription = checkpointId };
            var sql = $@"
                select Position from {_schema}.{_table} 
                where Subscription = @Subscription 
            "; 
            var position = await _connection.ExecuteScalarAsync<ulong?>(sql, parameters);
            return new Checkpoint(checkpointId, position);
        }

        public async ValueTask<Checkpoint> StoreCheckpoint(Checkpoint checkpoint, CancellationToken cancellationToken = default)
        {
            var sqlSelect = $@"
                select count(*) from {_schema}.{_table} 
                where Subscription = @Subscription 
            "; 
            var result = await _connection.ExecuteScalarAsync<long>(sqlSelect, new { Subscription = checkpoint.Id });

            var parameters = new { Subscription = checkpoint.Id, Position = (long?) checkpoint.Position };
            if (result != 0)
            {
                var sql = $@"
                    update {_schema}.{_table} 
                    set Position = @Position 
                    where Subscription = @Subscription
                ";
                await _connection.ExecuteAsync(sql, parameters);
            }
            else 
            {
                var sql = $@"
                    insert into {_schema}.{_table} (Subscription, Position) values (@Subscription, @Position)
                ";
                await _connection.ExecuteAsync(sql, parameters);
            }
            return checkpoint;
        }

        public async Task CreateSchemaIfNotExists() {
            var sql = $@"
                CREATE TABLE IF NOT EXISTS {_schema}.{_table} (
                    Subscription varchar(255),
                    Position bigint
                );
            ";
            await _connection.ExecuteAsync(sql);
        }

    }
}