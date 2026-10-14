// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.SqlServer;
using Microsoft.Data.SqlClient;

namespace Eventuous.Tests.SqlServer.Store;

[ClassDataSource<StoreFixture>]
public class SchemaTests(StoreFixture fixture) {
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldCoverStreamLookupAfterRepeatedInitialization(bool legacyConstraint) {
        var schema = new Schema(fixture.SchemaName);
        await using var connection = new SqlConnection(fixture.Container.GetConnectionString());
        await connection.OpenAsync();

        if (legacyConstraint) {
            await using var legacy = new SqlCommand(
                $"""
                 DROP INDEX UQ_StreamName ON {schema.SchemaName}.Streams;
                 ALTER TABLE {schema.SchemaName}.Streams ADD CONSTRAINT UQ_StreamName UNIQUE NONCLUSTERED (StreamName);
                 """,
                connection
            );
            await legacy.ExecuteNonQueryAsync();
        }

        await using var seed = new SqlCommand(
            $"INSERT INTO {schema.SchemaName}.Streams (StreamName, [Version]) OUTPUT INSERTED.StreamId VALUES (N'preserved-stream', 42);",
            connection
        );
        var streamId = (int)(await seed.ExecuteScalarAsync())!;

        // The first call upgrades the legacy constraint; subsequent calls must leave the covering index intact.
        await schema.CreateSchema(fixture.Container.GetConnectionString(), null, default);
        await schema.CreateSchema(fixture.Container.GetConnectionString(), null, default);

        await using var index = new SqlCommand(
            $"""
             SELECT COUNT(*)
             FROM sys.indexes i
             JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
             JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
             WHERE i.object_id = OBJECT_ID('{schema.SchemaName}.Streams')
               AND i.name = N'UQ_StreamName' AND i.is_unique = 1 AND i.is_unique_constraint = 0 AND i.type = 2
               AND ((c.name = N'StreamName' AND ic.key_ordinal = 1)
                 OR (c.name = N'Version' AND ic.is_included_column = 1));
             """,
            connection
        );
        await Assert.That((int)(await index.ExecuteScalarAsync())!).IsEqualTo(2);

        await using var preserved = new SqlCommand(
            $"SELECT [Version] FROM {schema.SchemaName}.Streams WHERE StreamId = @id AND StreamName = N'preserved-stream';",
            connection
        );
        preserved.Parameters.AddWithValue("@id", streamId);
        await Assert.That((int)(await preserved.ExecuteScalarAsync())!).IsEqualTo(42);

        await using var duplicate = new SqlCommand(
            $"INSERT INTO {schema.SchemaName}.Streams (StreamName) VALUES (N'preserved-stream');",
            connection
        );
        var error = await Assert.ThrowsAsync<SqlException>(() => duplicate.ExecuteNonQueryAsync());
        await Assert.That(error!.Number).IsEqualTo(2601);
    }
}
