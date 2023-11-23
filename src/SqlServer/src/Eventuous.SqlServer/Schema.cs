// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer;

public class Schema(string schema = Schema.DefaultSchema) {
    public const string DefaultSchema = "eventuous";

    public string AppendEvents        => $"{schema}.append_events";
    public string ReadStreamForwards  => $"{schema}.read_stream_forwards";
    public string ReadStreamBackwards => $"{schema}.read_stream_backwards";
    public string ReadStreamSub       => $"{schema}.read_stream_sub";
    public string ReadAllForwards     => $"{schema}.read_all_forwards";
    public string CheckStream         => $"{schema}.check_stream";
    public string StreamExists        => $"SELECT CAST(IIF(EXISTS(SELECT 1 FROM {schema}.Streams WHERE StreamName = (@name)), 1, 0) AS BIT)";
    public string GetCheckpointSql    => $"SELECT Position FROM {schema}.Checkpoints where Id=(@checkpointId)";
    public string AddCheckpointSql    => $"INSERT INTO {schema}.Checkpoints (Id) VALUES ((@checkpointId))";
    public string UpdateCheckpointSql => $"UPDATE {schema}.Checkpoints set Position=(@position) where Id=(@checkpointId)";

    static readonly Assembly Assembly = typeof(Schema).Assembly;

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
#if NET6_0
                var script = await reader.ReadToEndAsync().NoContext();
#else
                var script = await reader.ReadToEndAsync(cancellationToken).NoContext();
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
