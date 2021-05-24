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
    public class MsSqlCheckpointStore : ICheckpointStore {
        readonly ILogger<MsSqlCheckpointStore>? _log;
        readonly string _schema;
        readonly IDbConnection _connection;
        public MsSqlCheckpointStore(IDbConnection connection, string schema, ILogger<MsSqlCheckpointStore>? logger) {
            _connection = connection;
            _schema = schema ?? "dbo";
            _log = logger;
        }

        public async ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken = default)
        {
            _log?.LogDebug("[{CheckpointId}] Finding checkpoint...", checkpointId);

            var parameters = new { Subscription = checkpointId };
            var sql = $@"
                select Position from {_schema}.Checkpoints 
                where Subscription = @Subscription 
            "; 
            var position = await _connection.ExecuteScalarAsync<ulong?>(sql, parameters);
            return new Checkpoint(checkpointId, position);
        }

        public async ValueTask<Checkpoint> StoreCheckpoint(Checkpoint checkpoint, CancellationToken cancellationToken = default)
        {
            var sqlSelect = $@"
                select count(*) from {_schema}.Checkpoints 
                where Subscription = @Subscription 
            "; 
            var result = await _connection.ExecuteScalarAsync<long?>(sqlSelect, new { Subscription = checkpoint.Id });

            var parameters = new { Subscription = checkpoint.Id, Position = (long?) checkpoint.Position };
            if (result != 0)
            {
                var sql = $@"
                    update {_schema}.Checkpoints 
                    set Position = @Position 
                    where Subscription = @Subscription
                ";
                await _connection.ExecuteAsync(sql, parameters);
            }
            else 
            {
                var sql = $@"
                    insert into {_schema}.Checkpoints (Subscription, Position) values (@Subscription, @Position)
                ";
                await _connection.ExecuteAsync(sql, parameters);
            }
            return checkpoint;
        }

        public async Task CreateSchemaIfNotExists() {
            var sql = $@"
                IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = '{_schema}')
                BEGIN
                    EXEC('CREATE SCHEMA {_schema}')
                END
                IF OBJECT_ID('{_schema}.Checkpoints') IS NULL
                 BEGIN
                    create table {_schema}.Checkpoints (
                        Subscription varchar(255),
                        Position bigint
                    )
                 END
            ";
            await _connection.ExecuteAsync(sql);
        }

    }
}