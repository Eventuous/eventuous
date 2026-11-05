// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Eventuous.Postgresql;

/// <summary>
/// Instantiate a new Schema object with the specified schema name. The default schema name is "eventuous"
/// </summary>
/// <param name="schema"></param>
public class Schema(string schema = Schema.DefaultSchema) {
    public const string DefaultSchema = "eventuous";

    public static string GetStreamMessageTypeName(string schema = DefaultSchema) => $"{schema}.stream_message";

    public string Name => schema;

    public string StreamMessage       { get; } = GetStreamMessageTypeName(schema);
    public string AppendEvents        { get; } = $"select * from {schema}.append_events(@_stream_name, @_expected_version, @_created, @_messages)";
    public string ReadStreamForwards  { get; } = $"select * from {schema}.read_stream_forwards(@_stream_name, @_from_position, @_count)";
    public string ReadStreamBackwards { get; } = $"select * from {schema}.read_stream_backwards(@_stream_name, @_from_position, @_count)";
    public string ReadStreamSub       { get; } = $"select * from {schema}.read_stream_sub(@_stream_id, @_stream_name, @_from_position, @_count)";
    public string ReadAllForwards     { get; } = $"select * from {schema}.read_all_forwards(@_from_position, @_count)";
    public string CheckStream         { get; } = $"select * from {schema}.check_stream(@_stream_name, @_expected_version)";
    public string StreamExists        { get; } = $"select exists (select 1 from {schema}.streams where stream_name = (@name))";
    public string TruncateStream      { get; } = $"select * from {schema}.truncate_stream(@_stream_name, @_expected_version, @_position)";
    public string GetCheckpointSql    { get; } = $"select position from {schema}.checkpoints where id=(@checkpointId)";
    public string AddCheckpointSql    { get; } = $"insert into {schema}.checkpoints (id) values (@checkpointId)";
    public string UpdateCheckpointSql { get; } = $"update {schema}.checkpoints set position=(@position) where id=(@checkpointId)";
    public string TryInsertTombstone  { get; } = $"select {schema}.try_insert_tombstone(@_gap_position, @_stream_name, @_type, @_id)";

    static readonly Assembly Assembly = typeof(Schema).Assembly;

    public async Task CreateSchema(NpgsqlDataSource dataSource, ILogger<Schema>? log, CancellationToken cancellationToken = default) {
        log?.LogInformation("Creating schema {Schema}", schema);
        var names = Assembly.GetManifestResourceNames().Where(x => x.EndsWith(".sql")).OrderBy(x => x);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).NoContext();

        var transaction = await connection.BeginTransactionAsync(cancellationToken).NoContext();

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

                await using var cmd = new NpgsqlCommand(cmdScript, connection, transaction);

                await cmd.ExecuteNonQueryAsync(cancellationToken).NoContext();
            }
        } catch (Exception e) {
            log?.LogCritical(e, "Unable to initialize the database schema");
            await transaction.RollbackAsync(cancellationToken);

            throw;
        }

        await transaction.CommitAsync(cancellationToken).NoContext();
        await dataSource.ReloadTypesAsync(cancellationToken).NoContext();
        log?.LogInformation("Database schema initialized");
    }
}
