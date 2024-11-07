// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer;

public class Schema(string schema = Schema.DefaultSchema) {
    public const string DefaultSchema = "eventuous";

    public readonly string AppendEvents        = $"{schema}.append_events";
    public readonly string ReadStreamForwards  = $"{schema}.read_stream_forwards";
    public readonly string ReadStreamBackwards = $"{schema}.read_stream_backwards";
    public readonly string ReadStreamSub       = $"{schema}.read_stream_sub";
    public readonly string ReadAllForwards     = $"{schema}.read_all_forwards";
    public readonly string CheckStream         = $"{schema}.check_stream";
    public readonly string StreamExists        = $"SELECT CAST(IIF(EXISTS(SELECT 1 FROM {schema}.Streams WHERE StreamName = (@name)), 1, 0) AS BIT)";
    public readonly string TruncateStream      = $"{schema}.truncate_stream";
    public readonly string GetCheckpointSql    = $"SELECT Position FROM {schema}.Checkpoints where Id=(@checkpointId)";
    public readonly string AddCheckpointSql    = $"INSERT INTO {schema}.Checkpoints (Id) VALUES ((@checkpointId))";
    public readonly string UpdateCheckpointSql = $"UPDATE {schema}.Checkpoints set Position=(@position) where Id=(@checkpointId)";

    static readonly Assembly Assembly = typeof(Schema).Assembly;
    
    public string SchemaName => schema;

    [PublicAPI]
    public async Task CreateSchema(string connectionString, ILogger<Schema>? log, CancellationToken cancellationToken) {
        log?.LogInformation("Creating schema {Schema}", schema);

        var names = Assembly.GetManifestResourceNames()
            .Where(x => x.EndsWith(".sql"))
            .OrderBy(x => x);

        await using var connection  = await ConnectionFactory.GetConnection(connectionString, cancellationToken).NoContext();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).NoContext();

        try {
            foreach (var name in names) {
                log?.LogInformation("Executing {Script}", name);
                await using var stream = Assembly.GetManifestResourceStream(name);
                using var       reader = new StreamReader(stream!);
#if NET7_0_OR_GREATER
                var script = await reader.ReadToEndAsync(cancellationToken).NoContext();
#else
                var script = await reader.ReadToEndAsync().NoContext();
#endif
                var cmdScript = script.Replace("__schema__", schema);

                await using var cmd = new SqlCommand(cmdScript, connection, transaction);
                await cmd.ExecuteNonQueryAsync(cancellationToken).NoContext();
            }

            await transaction.CommitAsync(cancellationToken).NoContext();
        } catch (Exception e) {
            log?.LogCritical(e, "Unable to initialize the database schema");
            await transaction.RollbackAsync(cancellationToken);

            throw;
        }

        log?.LogInformation("Database schema initialized");
    }
}
