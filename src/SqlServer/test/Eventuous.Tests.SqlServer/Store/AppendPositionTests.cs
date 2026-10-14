// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Data.SqlClient;

namespace Eventuous.Tests.SqlServer.Store;

[ClassDataSource<StoreFixture>]
public class AppendPositionTests(StoreFixture fixture) {
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(10)]
    public async Task ShouldReturnBatchPositionForNewAndExistingStreams(int count) {
        await using var connection = new SqlConnection(fixture.Container.GetConnectionString());
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();

        // Reuse the session after another append: empty batches must not return an earlier identity.
        await AppendAndCheck("other-stream", 3, 2);
        await AppendAndCheck("other-stream", 0, 2);
        await AppendAndCheck("test-stream", count, count - 1);
        await AppendAndCheck("test-stream", count, count * 2 - 1);
        await transaction.CommitAsync();

        async Task AppendAndCheck(string stream, int batchSize, int expectedVersion) {
            await using var command = new SqlCommand(
                $"""
                 DECLARE @messages {fixture.SchemaName}.StreamMessage;
                 DECLARE @remaining INT = @count;
                 WHILE @remaining > 0
                 BEGIN
                     INSERT INTO @messages (message_id, message_type, json_data, json_metadata)
                     VALUES (NEWID(), N'TestEvent', @json, @json);
                     SET @remaining -= 1;
                 END;

                 EXEC {fixture.SchemaName}.append_events
                     @stream_name = @name, @expected_version = -2, @created = NULL, @messages = @messages;

                 SELECT MAX(m.GlobalPosition)
                 FROM {fixture.SchemaName}.Messages m
                 JOIN {fixture.SchemaName}.Streams s ON s.StreamId = m.StreamId
                 WHERE s.StreamName = @name;
                 """,
                connection,
                transaction
            );
            command.Parameters.AddWithValue("@count", batchSize);
            command.Parameters.AddWithValue("@name", stream);
            command.Parameters.AddWithValue("@json", "{}");

            await using var reader = await command.ExecuteReaderAsync();
            await Assert.That(await reader.ReadAsync()).IsTrue();
            await Assert.That(reader.GetInt32(0)).IsEqualTo(expectedVersion);
            long? position = reader.IsDBNull(1) ? null : reader.GetInt64(1);

            await Assert.That(await reader.NextResultAsync()).IsTrue();
            await Assert.That(await reader.ReadAsync()).IsTrue();
            long? expectedPosition = batchSize == 0 ? null : reader.GetInt64(0);
            await Assert.That(position).IsEqualTo(expectedPosition);
        }
    }
}
