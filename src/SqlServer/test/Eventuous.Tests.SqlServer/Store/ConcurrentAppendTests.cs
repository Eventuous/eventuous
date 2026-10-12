// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Sut.Domain;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Microsoft.Data.SqlClient;

namespace Eventuous.Tests.SqlServer.Store;

/// <summary>
/// Two appends with <see cref="ExpectedStreamVersion.Any"/> to one stream must both succeed. A third connection holds a lock
/// that stops both appends after check_stream reads the stream and before either one writes, so both appends read the same
/// stream state on every run instead of by chance.
/// </summary>
[ClassDataSource<StoreFixture>]
public class ConcurrentAppendTests {
    static readonly TimeSpan BlockTimeout = TimeSpan.FromSeconds(10);

    readonly StoreFixture _fixture;

    public ConcurrentAppendTests(StoreFixture fixture) {
        fixture.TypeMapper.RegisterKnownEventTypes(typeof(BookingEvents.BookingImported).Assembly);
        _fixture = fixture;
    }

    [Test]
    [Category("Store")]
    public async Task ShouldAppendWithAnyWhenTwoAppendsReadAnExistingStreamTogether() {
        var stream = Helpers.GetStreamName();
        await _fixture.AppendEvent(stream, Helpers.CreateEvent(), ExpectedStreamVersion.NoStream);

        // Stops every insert into Messages. check_stream reads only Streams, so it still runs.
        var failures = await AppendTwiceBehindGate(stream, $"SELECT COUNT(*) FROM {_fixture.SchemaName}.Messages WITH (TABLOCKX);");

        await Assert.That(failures).IsEmpty();
        await Assert.That(await CountEvents(stream)).IsEqualTo(3);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldNotLeaveGlobalPositionGapWhenTwoAppendsReadAnExistingStreamTogether() {
        // A rolled back insert still uses its identity value, and the gap it leaves holds the all-stream subscriptions.
        var stream = Helpers.GetStreamName();
        await _fixture.AppendEvent(stream, Helpers.CreateEvent(), ExpectedStreamVersion.NoStream);
        var identityBefore = await CurrentMessagesIdentity();

        var failures = await AppendTwiceBehindGate(stream, $"SELECT COUNT(*) FROM {_fixture.SchemaName}.Messages WITH (TABLOCKX);");

        await Assert.That(failures).IsEmpty();
        await Assert.That(await CountEvents(stream)).IsEqualTo(3);
        await Assert.That(await CurrentMessagesIdentity() - identityBefore).IsEqualTo(2);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldAppendWithAnyWhenTwoAppendsCreateTheSameStreamTogether() {
        var stream = Helpers.GetStreamName();

        // Stops the insert of the stream row. A read of the stream without lock hints still runs.
        var failures = await AppendTwiceBehindGate(
            stream,
            $"SELECT StreamId FROM {_fixture.SchemaName}.Streams WITH (UPDLOCK, HOLDLOCK) WHERE StreamName = @stream_name;"
        );

        await Assert.That(failures).IsEmpty();
        await Assert.That(await CountEvents(stream)).IsEqualTo(2);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldFailOneAppendWithoutGlobalPositionGapWhenTwoAppendsExpectTheSameVersion() {
        // Both appends expect version 0, so one must fail. It must fail in check_stream, before it inserts anything.
        var stream = Helpers.GetStreamName();
        await _fixture.AppendEvent(stream, Helpers.CreateEvent(), ExpectedStreamVersion.NoStream);
        var identityBefore = await CurrentMessagesIdentity();

        var failures = await AppendTwiceBehindGate(
            stream,
            $"SELECT COUNT(*) FROM {_fixture.SchemaName}.Messages WITH (TABLOCKX);",
            new ExpectedStreamVersion(0)
        );

        var failure = await Assert.That(failures).HasSingleItem();
        await Assert.That(failure).StartsWith("WrongExpectedVersion");
        await Assert.That(await CountEvents(stream)).IsEqualTo(2);
        await Assert.That(await CurrentMessagesIdentity() - identityBefore).IsEqualTo(1);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldNotWaitToCreateAStreamWhileAnotherStreamIsCreated() {
        // No other stream name sorts between the two names, so they share one gap in the index of stream names.
        var prefix = $"gap-{Guid.NewGuid():N}";

        var (completedWhileCreating, append) = await AppendWhileAnotherStreamIsCreated(new($"{prefix}-a"), new($"{prefix}-b"));

        await Assert.That(completedWhileCreating).IsTrue();
        await append;
    }

    [Test]
    [Category("Store")]
    public async Task ShouldNotWaitToAppendToAnExistingStreamWhileAnotherStreamIsCreated() {
        // The existing stream is the next name after the new stream in the index of stream names.
        var prefix   = $"gap-{Guid.NewGuid():N}";
        var existing = new StreamName($"{prefix}-z");
        await _fixture.AppendEvent(existing, Helpers.CreateEvent(), ExpectedStreamVersion.NoStream);

        var (completedWhileCreating, append) = await AppendWhileAnotherStreamIsCreated(new($"{prefix}-m"), existing);

        await Assert.That(completedWhileCreating).IsTrue();
        await append;
    }

    /// <summary>
    /// Creates <paramref name="created"/> in a transaction that stays open, and appends to <paramref name="appendedTo"/>
    /// while it is open. Returns whether the append completed before the transaction ended, and the append.
    /// </summary>
    async Task<(bool CompletedWhileCreating, Task Append)> AppendWhileAnotherStreamIsCreated(StreamName created, StreamName appendedTo) {
        await using var creator = new SqlConnection(_fixture.Container.GetConnectionString());
        await creator.OpenAsync();
        await using var creatorTransaction = (SqlTransaction)await creator.BeginTransactionAsync();

        await using (var create = new SqlCommand($"{_fixture.SchemaName}.check_stream", creator, creatorTransaction)) {
            create.CommandType = System.Data.CommandType.StoredProcedure;
            create.Parameters.AddWithValue("@stream_name", created.ToString());
            create.Parameters.AddWithValue("@expected_version", (int)ExpectedStreamVersion.Any.Value);
            create.Parameters.Add("@current_version", System.Data.SqlDbType.Int).Direction = System.Data.ParameterDirection.Output;
            create.Parameters.Add("@stream_id", System.Data.SqlDbType.Int).Direction   = System.Data.ParameterDirection.Output;
            await create.ExecuteNonQueryAsync();
        }

        var append = Task.Run(() => _fixture.AppendEvent(appendedTo, Helpers.CreateEvent(), ExpectedStreamVersion.Any));
        var completedWhileCreating = await Task.WhenAny(append, Task.Delay(BlockTimeout)) == append;

        // The rollback lets a blocked append finish, so the test does not hang.
        await creatorTransaction.RollbackAsync();

        return (completedWhileCreating, append);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldFailOneAppendWithNoStreamWhenTwoAppendsCreateTheSameStreamTogether() {
        var stream = Helpers.GetStreamName();

        var failures = await AppendTwiceBehindGate(
            stream,
            $"SELECT StreamId FROM {_fixture.SchemaName}.Streams WITH (UPDLOCK, HOLDLOCK) WHERE StreamName = @stream_name;",
            ExpectedStreamVersion.NoStream
        );

        var failure = await Assert.That(failures).HasSingleItem();
        await Assert.That(failure).StartsWith("WrongExpectedVersion");
        await Assert.That(await CountEvents(stream)).IsEqualTo(1);
    }

    [Test]
    [Category("Store")]
    public async Task ShouldAppendWithAnyWhenTwoAppendsCreateTheSameStreamWithDifferentCaseTogether() {
        // The default collation of SQL Server ignores case, so the unique index of stream names treats both names as one stream.
        var stream        = Helpers.GetStreamName();
        var differentCase = new StreamName(stream.ToString().ToUpperInvariant());

        var failures = await AppendTwiceBehindGate(
            stream,
            $"SELECT StreamId FROM {_fixture.SchemaName}.Streams WITH (UPDLOCK, HOLDLOCK) WHERE StreamName = @stream_name;",
            secondStream: differentCase
        );

        await Assert.That(failures).IsEmpty();
        await Assert.That(await CountEvents(stream)).IsEqualTo(2);
    }

    /// <summary>
    /// Holds the lock of <paramref name="gateSql"/>, starts two appends, waits until both are blocked, then releases the lock.
    /// The second append goes to <paramref name="secondStream"/> when it is given. Returns the message of each append that failed.
    /// </summary>
    async Task<List<string>> AppendTwiceBehindGate(
            StreamName            stream,
            string                gateSql,
            ExpectedStreamVersion? expectedVersion = null,
            StreamName?           secondStream    = null
        ) {
        var version = expectedVersion ?? ExpectedStreamVersion.Any;

        await using var gate = new SqlConnection(_fixture.Container.GetConnectionString());
        await gate.OpenAsync();
        await using var gateTransaction = (SqlTransaction)await gate.BeginTransactionAsync();

        await using (var hold = new SqlCommand(gateSql, gate, gateTransaction)) {
            hold.Parameters.AddWithValue("@stream_name", stream.ToString());
            await hold.ExecuteNonQueryAsync();
        }

        Task[] appends = [
            Task.Run(() => _fixture.AppendEvent(stream, Helpers.CreateEvent(), version)),
            Task.Run(() => _fixture.AppendEvent(secondStream ?? stream, Helpers.CreateEvent(), version))
        ];
        try {
            await WaitUntilAppendsAreBlocked(appends.Length);
        } catch {
            // Release the gate and let both appends finish, so that they do not outlive the test. The wait's exception is
            // the failure of the test, so the results of the appends are ignored.
            await gateTransaction.RollbackAsync();
            await Task.WhenAll(appends).ContinueWith(_ => { }, TaskScheduler.Default);

            throw;
        }

        await gateTransaction.RollbackAsync();

        var failures = new List<string>();

        foreach (var append in appends) {
            try {
                await append;
            } catch (Exception e) {
                failures.Add(e.GetBaseException().Message);
            }
        }

        return failures;
    }

    async Task WaitUntilAppendsAreBlocked(int count) {
        var sql = $"""
                   SELECT COUNT(*)
                   FROM sys.dm_exec_requests AS r
                   CROSS APPLY sys.dm_exec_input_buffer(r.session_id, r.request_id) AS b
                   WHERE r.blocking_session_id <> 0
                     AND b.event_info LIKE N'%{_fixture.SchemaName}.append_events%';
                   """;

        await using var connection = new SqlConnection(_fixture.Container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);

        var deadline = DateTime.UtcNow + BlockTimeout;

        while ((int)(await command.ExecuteScalarAsync())! < count) {
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"The {count} appends were not blocked within {BlockTimeout}.");

            await Task.Delay(20);
        }
    }

    async Task<decimal> CurrentMessagesIdentity() {
        await using var connection = new SqlConnection(_fixture.Container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new SqlCommand($"SELECT IDENT_CURRENT('{_fixture.SchemaName}.Messages');", connection);

        return (decimal)(await command.ExecuteScalarAsync())!;
    }

    async Task<int> CountEvents(StreamName stream) {
        var events = await _fixture.EventStore.ReadEvents(stream, StreamReadPosition.Start, 100, true, default);

        return events.Length;
    }
}
