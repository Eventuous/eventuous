// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using Eventuous.SqlServer;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer.Snapshots;

/// <summary>
/// Instantiate a new SnapshotSchema object with the specified schema name. The default schema name is "eventuous"
/// </summary>
/// <param name="schema"></param>
public class SnapshotSchema(string schema = SnapshotSchema.DefaultSchema) {
    public const string DefaultSchema = "eventuous";

    public string Name => schema;

    public string ReadSnapshot  => $"SELECT revision, event_type, json_data FROM {schema}.snapshots WHERE stream_name = @stream_name";
    public string WriteSnapshot  => $"MERGE {schema}.snapshots AS target USING (VALUES (@stream_name, @revision, @event_type, @json_data)) AS source (stream_name, revision, event_type, json_data) ON target.stream_name = source.stream_name WHEN MATCHED THEN UPDATE SET revision = source.revision, event_type = source.event_type, json_data = source.json_data, created = GETUTCDATE() WHEN NOT MATCHED THEN INSERT (stream_name, revision, event_type, json_data, created) VALUES (source.stream_name, source.revision, source.event_type, source.json_data, GETUTCDATE());";
    public string DeleteSnapshot => $"DELETE FROM {schema}.snapshots WHERE stream_name = @stream_name";

    static readonly Assembly Assembly = typeof(SnapshotSchema).Assembly;

    public async Task CreateSchema(string connectionString, ILogger<SnapshotSchema>? log, CancellationToken cancellationToken = default) {
        log?.LogInformation("Creating snapshot schema {Schema}", schema);
        const string scriptName = "Eventuous.SqlServer.Snapshots.Scripts.1_SnapshotSchema.sql";

        await using var connection = await ConnectionFactory.GetConnection(connectionString, cancellationToken).NoContext();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).NoContext();

        try {
            log?.LogInformation("Executing {Script}", scriptName);
            await using var stream = Assembly.GetManifestResourceStream(scriptName);
            if (stream == null) {
                throw new InvalidOperationException($"Embedded resource {scriptName} not found");
            }

            using var reader = new StreamReader(stream);

#if NET7_0_OR_GREATER
            var script = await reader.ReadToEndAsync(cancellationToken).NoContext();
#else
            var script = await reader.ReadToEndAsync().NoContext();
#endif
            var cmdScript = script.Replace("__schema__", schema);

            await using var cmd = new SqlCommand(cmdScript, connection, transaction);

            await cmd.ExecuteNonQueryAsync(cancellationToken).NoContext();
        } catch (Exception e) {
            log?.LogCritical(e, "Unable to initialize the snapshot database schema");
            await transaction.RollbackAsync(cancellationToken);

            throw;
        }

        await transaction.CommitAsync(cancellationToken).NoContext();
        log?.LogInformation("Snapshot database schema initialized");
    }
}

