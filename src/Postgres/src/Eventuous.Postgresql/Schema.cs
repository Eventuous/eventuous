// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using Npgsql;

namespace Eventuous.Postgresql;

public class Schema {
    readonly string _schema;

    public const string DefaultSchema = "eventuous";

    public Schema(string schema = DefaultSchema) => _schema = schema;

    public string StreamMessage      => $"{_schema}.stream_message";
    public string AppendEvents       => $"{_schema}.append_events";
    public string ReadStreamForwards => $"{_schema}.read_stream_forwards";
    public string ReadStreamSub      => $"{_schema}.read_stream_sub";
    public string ReadAllForwards    => $"{_schema}.read_all_forwards";
    public string CheckStream        => $"{_schema}.check_stream";
    public string StreamExists       => $"select exists (select 1 from {_schema}.streams where stream_name = (@name))";
    public string GetCheckpointSql   => $"select position from {_schema}.checkpoints where id=(@checkpointId)";
    public string AddCheckpointSql   => $"insert into {_schema}.checkpoints (id) values (@checkpointId)";
    public string UpdateCheckpointSql
        => $"update {_schema}.checkpoints set position=(@position) where id=(@checkpointId)";

    static readonly Assembly Assembly = typeof(Schema).Assembly;

    public async Task CreateSchema(GetPostgresConnection getConnection) {
        var names = Assembly.GetManifestResourceNames()
            .Where(x => x.EndsWith(".sql"))
            .OrderBy(x => x);

        await using var connection = getConnection();
        await connection.OpenAsync().NoContext();
        await using var transaction = await connection.BeginTransactionAsync().NoContext();

        foreach (var name in names) {
            await using var stream    = Assembly.GetManifestResourceStream(name);
            using var       reader    = new StreamReader(stream!);
            var             script    = await reader.ReadToEndAsync().NoContext();
            var             cmdScript = script.Replace("__schema__", _schema);
            await using var cmd       = new NpgsqlCommand(cmdScript, connection, transaction);

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
