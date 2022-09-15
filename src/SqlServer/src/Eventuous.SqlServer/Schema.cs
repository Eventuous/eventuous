// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using Microsoft.Data.SqlClient;

namespace Eventuous.SqlServer;

public class Schema {
    readonly string _schema;

    public Schema(string schema) => _schema = schema;

    public string StreamMessage      => $"{_schema}.stream_message";
    public string AppendEvents       => $"{_schema}.append_events";
    public string ReadStreamForwards => $"{_schema}.read_stream_forwards";
    public string ReadStreamSub      => $"{_schema}.read_stream_sub";
    public string ReadAllForwards    => $"{_schema}.read_all_forwards";
    public string CheckStream        => $"{_schema}.check_stream";
    public string StreamExists       => $"SELECT CAST(IIF(EXISTS(SELECT 1 FROM {_schema}.streams WHERE stream_name = (@name)), 1, 0) AS BIT)";
    public string GetCheckpointSql   => $"select position from {_schema}.checkpoints where id=(@checkpointId)";
    public string AddCheckpointSql   => $"insert into {_schema}.checkpoints (id) values ((@checkpointId))";
    public string UpdateCheckpointSql
        => $"update {_schema}.checkpoints set position=(@position) where id=(@checkpointId)";

    static readonly Assembly Assembly = typeof(Schema).Assembly;

    public async Task CreateSchema(GetSqlServerConnection getConnection) {
        var names = Assembly.GetManifestResourceNames()
            .Where(x => x.EndsWith(".sql"))
            .OrderBy(x => x);

        await using var connection = getConnection();
        await connection.OpenAsync().NoContext();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync().NoContext();

        foreach (var name in names) {
            await using var stream    = Assembly.GetManifestResourceStream(name);
            using var       reader    = new StreamReader(stream!);
            var             script    = await reader.ReadToEndAsync().NoContext();
            var             cmdScript = script.Replace("__schema__", _schema);
            await using var cmd       = new SqlCommand(cmdScript, connection, transaction);
            await cmd.ExecuteNonQueryAsync().NoContext();
        }

        await transaction.CommitAsync().NoContext();
    }
}
