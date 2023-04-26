// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;

namespace Eventuous.Postgresql;

public class Schema {
    readonly string _schema;

    public const string DefaultSchema = "eventuous";

    /// <summary>
    /// Instantiate a new Schema object with the specified schema name. Default schema name is "eventuous"
    /// </summary>
    /// <param name="schema"></param>
    public Schema(string schema = DefaultSchema)
        => _schema = schema;

    public string StreamMessage       => $"{_schema}.stream_message";
    public string AppendEvents        => $"select * from {_schema}.append_events(@_stream_name, @_expected_version, @_created, @_messages)";
    public string ReadStreamForwards  => $"select * from {_schema}.read_stream_forwards(@_stream_name, @_from_position, @_count)";
    public string ReadStreamSub       => $"select * from {_schema}.read_stream_sub(@_stream_id, @_from_position, @_count)";
    public string ReadAllForwards     => $"select * from {_schema}.read_all_forwards(@_from_position, @_count)";
    public string CheckStream         => $"select * from {_schema}.check_stream(@_stream_name, @_expected_version)";
    public string StreamExists        => $"select exists (select 1 from {_schema}.streams where stream_name = (@name))";
    public string GetCheckpointSql    => $"select position from {_schema}.checkpoints where id=(@checkpointId)";
    public string AddCheckpointSql    => $"insert into {_schema}.checkpoints (id) values (@checkpointId)";
    public string UpdateCheckpointSql => $"update {_schema}.checkpoints set position=(@position) where id=(@checkpointId)";

    static readonly Assembly Assembly = typeof(Schema).Assembly;

    public async Task CreateSchema(NpgsqlDataSource dataSource) {
        var names = Assembly.GetManifestResourceNames()
            .Where(x => x.EndsWith(".sql"))
            .OrderBy(x => x);

        await using var connection = await dataSource.OpenConnectionAsync().NoContext();

        var transaction = await connection.BeginTransactionAsync().NoContext();

        foreach (var name in names) {
            await using var stream = Assembly.GetManifestResourceStream(name);
            using var       reader = new StreamReader(stream!);

            var script    = await reader.ReadToEndAsync().NoContext();
            var cmdScript = script.Replace("__schema__", _schema);

            await using var cmd = new NpgsqlCommand(cmdScript, connection, transaction);

            try {
                await cmd.ExecuteNonQueryAsync().NoContext();
            }
            catch (Exception e) {
                Console.WriteLine(e);
                throw;
            }
        }

        await transaction.CommitAsync().NoContext();
    }
}
