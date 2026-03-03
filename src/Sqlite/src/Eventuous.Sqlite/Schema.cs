// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Eventuous.Sqlite;

public class Schema(string schema = Schema.DefaultSchema) {
    public const string DefaultSchema = "eventuous";

    public readonly string StreamsTable     = $"{schema}_streams";
    public readonly string MessagesTable    = $"{schema}_messages";
    public readonly string CheckpointsTable = $"{schema}_checkpoints";

    public readonly string StreamExists        = $"SELECT EXISTS(SELECT 1 FROM {schema}_streams WHERE stream_name = @name)";
    public readonly string GetCheckpointSql    = $"SELECT position FROM {schema}_checkpoints WHERE id = @checkpointId";
    public readonly string AddCheckpointSql    = $"INSERT INTO {schema}_checkpoints (id) VALUES (@checkpointId)";
    public readonly string UpdateCheckpointSql = $"UPDATE {schema}_checkpoints SET position = @position WHERE id = @checkpointId";

    static readonly Assembly Assembly = typeof(Schema).Assembly;

    public string SchemaName => schema;

    [PublicAPI]
    public async Task CreateSchema(string connectionString, ILogger<Schema>? log, CancellationToken cancellationToken) {
        log?.LogInformation("Creating schema {Schema}", schema);

        var names = Assembly.GetManifestResourceNames()
            .Where(x => x.EndsWith(".sql"))
            .OrderBy(x => x);

        await using var connection  = await ConnectionFactory.GetConnection(connectionString, cancellationToken).NoContext();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken).NoContext();

        try {
            foreach (var name in names) {
                log?.LogInformation("Executing {Script}", name);
                await using var stream = Assembly.GetManifestResourceStream(name);
                using var       reader = new StreamReader(stream!);
                var script = await reader.ReadToEndAsync(cancellationToken).NoContext();
                var cmdScript = script.Replace("__schema__", schema);

                await using var cmd = new SqliteCommand(cmdScript, connection, transaction);
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
